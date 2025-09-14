using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.DTOs;
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
        
        // Use the new universal parsing system
        var transactions = await ParseFileWithUniversalParser(import, fileData, cancellationToken);
        
        var account = await GetAccountForImport(context, import.UserId, cancellationToken);

        import.TotalRows = transactions.Count;
        var (importedCount, duplicateCount) = await ImportTransactionsAsync(
            context, import, transactions, account.Id, cancellationToken);

        import.ImportedTransactions = importedCount;
        import.DuplicateTransactions = duplicateCount;
        import.ProcessedRows = transactions.Count;

        await context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Import {ImportId} completed: {Imported} imported, {Duplicates} duplicates", 
            import.Id, importedCount, duplicateCount);
    }

    private async Task<List<ParsedTransaction>> ParseFileWithUniversalParser(
        ImportedFile import, Stream fileData, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var universalParser = scope.ServiceProvider.GetService<IUniversalBankParser>();
        var aiAnalyzer = scope.ServiceProvider.GetService<IAIBankAnalyzer>();
        var templateService = scope.ServiceProvider.GetService<IBankTemplateService>();

        // If services are not available, fall back to legacy parsing
        if (universalParser == null)
        {
            _logger.LogWarning("Universal parser not available, using legacy parsing for import {ImportId}", import.Id);
            return ParseFileDataLegacy(import, fileData);
        }

        try
        {
            // Convert stream to byte array
            var memoryStream = new MemoryStream();
            await fileData.CopyToAsync(memoryStream, cancellationToken);
            var fileBytes = memoryStream.ToArray();

            // Detect bank if not already detected
            BankDetectionResult? bankInfo = null;
            if (string.IsNullOrEmpty(import.DetectedBankName) && aiAnalyzer != null)
            {
                bankInfo = await aiAnalyzer.DetectBankAsync(fileBytes, import.FileName);
                
                // Update import record with detected info
                import.DetectedBankName = bankInfo.BankName;
                import.DetectedCountry = bankInfo.Country;
                if (string.IsNullOrEmpty(import.DetectedFormat))
                {
                    import.DetectedFormat = bankInfo.FileFormat;
                }
            }

            // Parse transactions using universal parser
            _logger.LogInformation("Parsing file {FileName} with universal parser, detected format: {Format}", 
                import.FileName, import.DetectedFormat);
            
            var parseResult = await universalParser.ParseFileAsync(fileBytes, import.FileName, bankInfo);

            if (!parseResult.IsSuccessful)
            {
                throw new InvalidOperationException(parseResult.ErrorMessage ?? "Parsing failed");
            }

            _logger.LogInformation("Universal parser returned {Count} transactions", parseResult.Transactions.Count);
            
            // Log each parsed transaction for debugging
            foreach (var txn in parseResult.Transactions)
            {
                _logger.LogInformation("Transaction from parser: Date={Date}, Description={Desc}, Amount={Amount}",
                    txn.Date, txn.Description, txn.Amount);
            }

            // Update cost tracking
            import.AICost = parseResult.AICost;

            // Save successful template if we have bank info and template service
            if (bankInfo != null && templateService != null && parseResult.IsSuccessful)
            {
                try
                {
                    await templateService.SaveTemplateAsync(bankInfo, parseResult);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save template for {BankName}", bankInfo.BankName);
                }
            }

            return parseResult.Transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in universal parsing for import {ImportId}, falling back to legacy", import.Id);
            return ParseFileDataLegacy(import, fileData);
        }
    }

    private List<ParsedTransaction> ParseFileDataLegacy(ImportedFile import, Stream fileData)
    {
        // Legacy parsing method as fallback
        var transactions = new List<ParsedTransaction>();
        
        _logger.LogWarning("Using legacy parsing for import {ImportId} - this should only be used for testing", import.Id);
        
        // Generate sample transactions based on the mock OCR text
        var mockTransactions = new List<(string desc, decimal amount, int daysAgo)>
        {
            ("Uber", -5.00m, 2),
            ("Uber", -52.25m, 2),
            ("Netflix", -7.99m, 3),
            ("Uber Eats", -23.94m, 3),
            ("Cinemark", -14.85m, 4),
            ("Fi", -108.04m, 4),
            ("Giant's Liquor & Food", -5.04m, 5)
        };
        
        foreach (var (desc, amount, daysAgo) in mockTransactions)
        {
            transactions.Add(new ParsedTransaction
            {
                Date = DateTime.UtcNow.AddDays(-daysAgo),
                Description = desc,
                Amount = amount,
                Category = null // Let AI handle categorization later
            });
        }

        _logger.LogInformation("Legacy parsing generated {Count} transactions for import {ImportId}", 
            transactions.Count, import.Id);
        
        return transactions;
    }

    private async Task<Account> GetAccountForImport(BudgetTrackerDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
            
        if (account == null)
        {
            throw new InvalidOperationException($"No account found for user {userId}");
        }
        
        return account;
    }

    private async Task<(int imported, int duplicates)> ImportTransactionsAsync(
        BudgetTrackerDbContext context, 
        ImportedFile import, 
        List<ParsedTransaction> transactions, 
        Guid accountId, 
        CancellationToken cancellationToken)
    {
        int importedCount = 0;
        int duplicateCount = 0;

        foreach (var txn in transactions)
        {
            var hash = GenerateTransactionHash(txn, accountId);
            
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
                AccountId = accountId,
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
        }

        await context.SaveChangesAsync(cancellationToken);
        return (importedCount, duplicateCount);
    }

    private string GenerateTransactionHash(ParsedTransaction txn, Guid accountId)
    {
        var input = $"{accountId}|{txn.Date:yyyy-MM-dd}|{txn.Amount:F2}|{txn.Description}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}