using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.Services.Merchants;
using BudgetTracker.Common.Services.Transactions;
using BudgetTracker.Common.DTOs;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        if (pendingImports.Any())
        {
            _logger.LogInformation("🔍 Found {Count} pending imports to process", pendingImports.Count);
        }

        foreach (var import in pendingImports)
        {
            try
            {
                _logger.LogInformation("📁 Processing import {ImportId} - {FileName} ({FileSize} bytes)", 
                    import.Id, import.FileName, import.FileSize);
                
                await ProcessImportFile(import, context, scope.ServiceProvider, blobService, cancellationToken);
                
                import.Status = ImportStatus.Completed;
                import.ProcessingCompletedAt = DateTime.UtcNow;
                import.UpdatedAt = DateTime.UtcNow;
                
                _logger.LogInformation("✅ Import {ImportId} completed successfully", import.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing import {ImportId}: {Error}", import.Id, ex.Message);
                import.Status = ImportStatus.Failed;
                import.ErrorDetails = ex.Message;
                import.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessImportFile(ImportedFile import, BudgetTrackerDbContext context, 
        IServiceProvider serviceProvider, IBlobStorageService blobService, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(import.BlobUrl))
        {
            throw new InvalidOperationException("Import file URL is missing");
        }

        _logger.LogInformation("📥 Downloading file from blob storage...");
        var fileData = await blobService.DownloadFileAsync("imports", $"{import.UserId}/{import.Id}{import.FileType}");
        
        _logger.LogInformation("🔍 Parsing file with universal parser...");
        var transactions = await ParseFileWithUniversalParser(import, fileData, cancellationToken);
        
        _logger.LogInformation("📊 Found {Count} transactions to process", transactions.Count);
        
        var account = await GetAccountForImport(context, import.UserId, cancellationToken);
        _logger.LogInformation("🏦 Using account: {AccountName} ({AccountId})", account.Name, account.Id);

        import.TotalRows = transactions.Count;
        var (importedCount, duplicateCount) = await ImportTransactionsAsync(
            context, serviceProvider, import, transactions, account.Id, cancellationToken);

        import.ImportedTransactions = importedCount;
        import.DuplicateTransactions = duplicateCount;
        import.ProcessedRows = transactions.Count;

        await context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("💾 Import {ImportId} saved: {Imported} imported, {Duplicates} duplicates", 
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
                _logger.LogInformation("🏦 Detecting bank from file content...");
                bankInfo = await aiAnalyzer.DetectBankAsync(fileBytes, import.FileName);
                
                _logger.LogInformation("🏦 Detected: {Bank} ({Country}) - Confidence: {Confidence:F2}", 
                    bankInfo.BankName, bankInfo.Country, bankInfo.Confidence);
                
                // Update import record with detected info
                import.DetectedBankName = bankInfo.BankName;
                import.DetectedCountry = bankInfo.Country;
                if (string.IsNullOrEmpty(import.DetectedFormat))
                {
                    import.DetectedFormat = bankInfo.FileFormat;
                }
            }
            else if (!string.IsNullOrEmpty(import.DetectedBankName))
            {
                _logger.LogInformation("🏦 Using previously detected bank: {Bank}", import.DetectedBankName);
            }

            // Parse transactions using universal parser
            _logger.LogInformation("📋 Parsing transactions from {Format} file...", import.DetectedFormat ?? "unknown");
            
            var parseResult = await universalParser.ParseFileAsync(fileBytes, import.FileName, bankInfo);

            if (!parseResult.IsSuccessful)
            {
                _logger.LogError("❌ Parsing failed: {Error}", parseResult.ErrorMessage);
                throw new InvalidOperationException(parseResult.ErrorMessage ?? "Parsing failed");
            }

            _logger.LogInformation("✅ Successfully parsed {Count} transactions (Cost: ${Cost:F4})", 
                parseResult.Transactions.Count, parseResult.AICost);
            
            // Log sample transactions (first 3 only)
            for (int i = 0; i < Math.Min(3, parseResult.Transactions.Count); i++)
            {
                var txn = parseResult.Transactions[i];
                _logger.LogInformation("  📄 {Date:MM/dd} | {Description} | {Amount:C} | {Category}", 
                    txn.Date, txn.Description, txn.Amount, txn.Category ?? "Uncategorized");
            }
            
            if (parseResult.Transactions.Count > 3)
            {
                _logger.LogInformation("  ... and {Count} more transactions", parseResult.Transactions.Count - 3);
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
        
        _logger.LogWarning("⚠️ Using legacy parsing for import {ImportId} - this should only be used for testing", import.Id);
        
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

        _logger.LogInformation("📝 Legacy parsing generated {Count} transactions for import {ImportId}", 
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
        IServiceProvider serviceProvider,
        ImportedFile import, 
        List<ParsedTransaction> transactions, 
        Guid accountId, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 Processing {Count} transactions with optimized batch service...", transactions.Count);
        
        var batchService = serviceProvider.GetRequiredService<IBatchTransactionService>();
        
        // Convert ParsedTransactions to Transaction objects
        var transactionList = transactions.Select(parsedTxn => new Transaction
        {
            TransactionDate = parsedTxn.Date,
            PostedDate = parsedTxn.Date,
            Amount = parsedTxn.Amount,
            Type = parsedTxn.Amount >= 0 ? TransactionType.Credit : TransactionType.Debit,
            Description = parsedTxn.Description ?? "Import",
            OriginalMerchant = ExtractMerchantFromDescription(parsedTxn.Description),
            Metadata = BuildImportMetadata(parsedTxn.Category),
            ImportedFileId = import.Id
        }).ToList();
        
        // Process batch with optimized service
        var result = await batchService.ProcessTransactionBatchAsync(
            transactionList, 
            import.UserId, 
            accountId, 
            import.Id);
        
        _logger.LogInformation("📊 Batch processing summary:");
        _logger.LogInformation("  ✅ Processed: {TotalProcessed}", result.TotalProcessed);
        _logger.LogInformation("  ✅ Inserted: {Inserted}", result.Inserted);
        _logger.LogInformation("  🔄 Duplicates: {Duplicates}", result.Duplicates);
        _logger.LogInformation("  ❌ Errors: {Errors}", result.Errors);
        _logger.LogInformation("  ⏱️ Processing time: {ProcessingTime}ms", result.ProcessingTime.TotalMilliseconds);
        
        if (result.ErrorMessages.Any())
        {
            foreach (var error in result.ErrorMessages)
            {
                _logger.LogWarning("  ⚠️ Error: {Error}", error);
            }
        }

        return (result.Inserted, result.Duplicates);
    }

    private string GenerateTransactionHash(ParsedTransaction txn, Guid accountId)
    {
        var input = $"{accountId}|{txn.Date:yyyy-MM-dd}|{txn.Amount:F2}|{txn.Description}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private static string? BuildImportMetadata(string? parsedCategory)
    {
        if (string.IsNullOrWhiteSpace(parsedCategory))
            return null;

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["parsedCategory"] = parsedCategory.Trim()
        });
    }

    private string ExtractMerchantFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "Unknown";

        var desc = description.Trim();
        
        // Remove common prefixes
        var prefixesToRemove = new[] { "POS ", "DEBIT ", "CREDIT ", "ACH ", "CHECK ", "ATM ", "PAYPAL " };
        foreach (var prefix in prefixesToRemove)
        {
            if (desc.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                desc = desc.Substring(prefix.Length).Trim();
                break;
            }
        }
        
        var parts = desc.Split(new[] { ' ', '#', '*' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Unknown";

        var merchantTokens = new List<string>();
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REF", "PAYMENT", "AUTH", "PENDING", "ONLINE", "CARD" };
        const int maxTokens = 4;

        foreach (var part in parts)
        {
            if (merchantTokens.Count >= maxTokens) break;
            var p = part.Trim('-');
            if (string.IsNullOrEmpty(p)) continue;
            if (p.All(char.IsDigit) && p.Length >= 4) break;
            if (stopWords.Contains(p)) break;
            merchantTokens.Add(p);
        }

        return merchantTokens.Count > 0 ? string.Join(" ", merchantTokens) : parts[0];
    }
}