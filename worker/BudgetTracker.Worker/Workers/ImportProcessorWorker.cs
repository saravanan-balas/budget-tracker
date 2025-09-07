using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Worker.Workers;

public class ImportProcessorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImportProcessorWorker> _logger;

    public ImportProcessorWorker(IServiceProvider serviceProvider, ILogger<ImportProcessorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingImports(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing imports");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task ProcessPendingImports(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();
        var blobService = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

        var pendingImports = await context.ImportedFiles
            .Where(f => f.Status == ImportStatus.Processing)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var import in pendingImports)
        {
            try
            {
                _logger.LogInformation("Processing import {ImportId} for user {UserId}", import.Id, import.UserId);
                
                await ProcessImportFile(import, context, blobService, cancellationToken);
                
                import.Status = ImportStatus.Completed;
                import.ProcessingCompletedAt = DateTime.UtcNow;
                import.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing import {ImportId}", import.Id);
                import.Status = ImportStatus.Failed;
                import.ErrorDetails = ex.Message;
                import.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessImportFile(ImportedFile import, BudgetTrackerDbContext context, 
        IBlobStorageService blobService, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(import.BlobUrl))
        {
            throw new InvalidOperationException("Import file URL is missing");
        }

        var fileData = await blobService.DownloadFileAsync("imports", $"{import.UserId}/{import.Id}{import.FileType}");
        
        var transactions = ParseFileData(import, fileData);
        
        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.UserId == import.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Account not found");

        import.TotalRows = transactions.Count;
        var importedCount = 0;
        var duplicateCount = 0;

        foreach (var txn in transactions)
        {
            var hash = GenerateTransactionHash(txn, account.Id);
            
            var exists = await context.Transactions
                .AnyAsync(t => t.ImportHash == hash && t.UserId == import.UserId, cancellationToken);

            if (exists)
            {
                duplicateCount++;
                continue;
            }

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = import.UserId,
                AccountId = account.Id,
                TransactionDate = txn.Date,
                PostedDate = txn.Date,
                Amount = txn.Amount,
                Type = txn.Amount < 0 ? TransactionType.Debit : TransactionType.Credit,
                OriginalMerchant = txn.Description,
                Description = txn.Description,
                ImportHash = hash,
                ImportedFileId = import.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Transactions.Add(transaction);
            importedCount++;
            import.ProcessedRows++;
        }

        import.ImportedTransactions = importedCount;
        import.DuplicateTransactions = duplicateCount;

        await context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Import {ImportId} completed: {Imported} imported, {Duplicates} duplicates", 
            import.Id, importedCount, duplicateCount);
    }

    private List<ParsedTransaction> ParseFileData(ImportedFile import, Stream fileData)
    {
        var transactions = new List<ParsedTransaction>();
        
        transactions.Add(new ParsedTransaction
        {
            Date = DateTime.UtcNow.AddDays(-5),
            Description = "Sample Transaction",
            Amount = -45.99m
        });

        return transactions;
    }

    private string GenerateTransactionHash(ParsedTransaction txn, Guid accountId)
    {
        var input = $"{accountId}|{txn.Date:yyyy-MM-dd}|{txn.Amount:F2}|{txn.Description}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private class ParsedTransaction
    {
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}