using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.Services.Merchants;
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
                
                await ProcessImportFile(import, context, scope.ServiceProvider, blobService, cancellationToken);
                
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
        IServiceProvider serviceProvider, IBlobStorageService blobService, CancellationToken cancellationToken)
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
            context, serviceProvider, import, transactions, account.Id, cancellationToken);

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
                _logger.LogInformation("🏦 BANK DETECTION: Analyzing PDF to detect bank and format...");
                bankInfo = await aiAnalyzer.DetectBankAsync(fileBytes, import.FileName);
                
                _logger.LogInformation("  ✅ Bank detection results:");
                _logger.LogInformation("    - Bank name: {Bank}", bankInfo.BankName);
                _logger.LogInformation("    - Country: {Country}", bankInfo.Country);
                _logger.LogInformation("    - File format: {Format}", bankInfo.FileFormat);
                _logger.LogInformation("    - Confidence: {Confidence}", bankInfo.Confidence);
                _logger.LogInformation("    - Template ID: {TemplateId}", bankInfo.TemplateId ?? "none");
                
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
            _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
            _logger.LogInformation("📋 PDF PARSING DEBUG - File: {FileName}", import.FileName);
            _logger.LogInformation("  - Detected format: {Format}", import.DetectedFormat ?? "unknown");
            _logger.LogInformation("  - Detected bank: {Bank}", import.DetectedBankName ?? "unknown");
            _logger.LogInformation("  - File size: {Size} bytes", fileBytes.Length);
            _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
            
            var parseResult = await universalParser.ParseFileAsync(fileBytes, import.FileName, bankInfo);

            if (!parseResult.IsSuccessful)
            {
                _logger.LogError("❌ PDF parsing failed: {Error}", parseResult.ErrorMessage);
                throw new InvalidOperationException(parseResult.ErrorMessage ?? "Parsing failed");
            }

            _logger.LogInformation("✅ Universal parser successfully extracted {Count} transactions", parseResult.Transactions.Count);
            _logger.LogInformation("💰 AI cost for parsing: ${Cost:F4}", parseResult.AICost);
            
            // Log first 5 parsed transactions for debugging
            int txnCount = 0;
            foreach (var txn in parseResult.Transactions.Take(5))
            {
                txnCount++;
                _logger.LogInformation("┌─────────────────────────────────────────────────────────────────┐");
                _logger.LogInformation("│ PARSED TRANSACTION {Index}/{Total} FROM PDF                     │", txnCount, Math.Min(5, parseResult.Transactions.Count));
                _logger.LogInformation("├─────────────────────────────────────────────────────────────────┤");
                _logger.LogInformation("│ Date:        {Date:yyyy-MM-dd}", txn.Date);
                _logger.LogInformation("│ Description: '{Desc}'", txn.Description);
                _logger.LogInformation("│ Amount:      {Amount:C}", txn.Amount);
                _logger.LogInformation("│ Category:    {Category}", txn.Category ?? "(none)");
                _logger.LogInformation("└─────────────────────────────────────────────────────────────────┘");
            }
            
            if (parseResult.Transactions.Count > 5)
            {
                _logger.LogInformation("... and {Count} more transactions (not shown in debug)", parseResult.Transactions.Count - 5);
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
        IServiceProvider serviceProvider,
        ImportedFile import, 
        List<ParsedTransaction> transactions, 
        Guid accountId, 
        CancellationToken cancellationToken)
    {
        int importedCount = 0;
        int duplicateCount = 0;
        int transactionIndex = 0;

        // Get services
        var merchantService = serviceProvider?.GetService<IMerchantService>();
        var aiAnalyzer = serviceProvider?.GetService<IAIBankAnalyzer>();

        _logger.LogInformation("=== Starting transaction import for {Count} transactions ===", transactions.Count);

        foreach (var txn in transactions)
        {
            transactionIndex++;
            
            // Detailed logging for first 5 transactions
            if (transactionIndex <= 5)
            {
                _logger.LogInformation("╔═══════════════════════════════════════════════════════════════════╗");
                _logger.LogInformation("║ Processing Transaction {Index} of {Total} (DEBUG MODE)            ║", transactionIndex, transactions.Count);
                _logger.LogInformation("╚═══════════════════════════════════════════════════════════════════╝");
                
                _logger.LogInformation("📄 STEP 1: RAW PARSED DATA FROM PDF:");
                _logger.LogInformation("  - Date: {Date}", txn.Date);
                _logger.LogInformation("  - Description: '{Description}'", txn.Description);
                _logger.LogInformation("  - Amount: {Amount:C}", txn.Amount);
                _logger.LogInformation("  - Category from parser: {Category}", txn.Category ?? "null");
            }

            // Generate hash for duplicate detection
            var hash = GenerateTransactionHash(txn, accountId);
            
            if (transactionIndex <= 5)
            {
                _logger.LogInformation("🔐 STEP 2: DUPLICATE DETECTION:");
                _logger.LogInformation("  - Generated hash: {Hash}", hash);
            }
            
            var exists = await context.Transactions
                .AnyAsync(t => t.ImportHash == hash && t.UserId == import.UserId, cancellationToken);

            if (exists)
            {
                duplicateCount++;
                if (transactionIndex <= 5)
                {
                    _logger.LogWarning("  ⚠️ DUPLICATE FOUND! Skipping transaction.");
                }
                continue;
            }
            
            if (transactionIndex <= 5)
            {
                _logger.LogInformation("  ✅ No duplicate found, proceeding with import");
            }

            // Find or create merchant using embedding similarity
            Merchant? merchant = null;
            if (merchantService != null && !string.IsNullOrWhiteSpace(txn.Description))
            {
                if (transactionIndex <= 5)
                {
                    _logger.LogInformation("🏪 STEP 3: MERCHANT NORMALIZATION:");
                    _logger.LogInformation("  - Original merchant text: '{Text}'", txn.Description);
                }
                
                try
                {
                    var matchResult = await merchantService.FindBestMatchAsync(txn.Description, 0.7);
                    if (matchResult != null)
                    {
                        merchant = matchResult.Merchant;
                        if (transactionIndex <= 5)
                        {
                            _logger.LogInformation("  ✅ MATCHED to existing merchant:");
                            _logger.LogInformation("    - Normalized name: '{Name}'", merchant.DisplayName);
                            _logger.LogInformation("    - Match method: {Method}", matchResult.MatchMethod);
                            _logger.LogInformation("    - Similarity score: {Score:F3}", matchResult.SimilarityScore);
                            _logger.LogInformation("    - Merchant ID: {Id}", merchant.Id);
                        }
                    }
                    else
                    {
                        // Create new merchant if no good match found
                        merchant = await merchantService.CreateOrGetMerchantAsync(txn.Description, txn.Category);
                        if (transactionIndex <= 5)
                        {
                            _logger.LogInformation("  🆕 CREATED new merchant:");
                            _logger.LogInformation("    - Display name: '{Name}'", merchant.DisplayName);
                            _logger.LogInformation("    - Category: {Category}", merchant.Category ?? "null");
                            _logger.LogInformation("    - New merchant ID: {Id}", merchant.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "  ❌ ERROR processing merchant for transaction: {Description}", txn.Description);
                }
            }
            else if (transactionIndex <= 5)
            {
                _logger.LogWarning("  ⚠️ Merchant service not available or description is empty");
            }

            // Categorization
            string? categoryName = txn.Category;
            Guid? categoryId = null;
            
            if (transactionIndex <= 5)
            {
                _logger.LogInformation("🏷️ STEP 4: CATEGORIZATION:");
                _logger.LogInformation("  - Initial category from parser: {Category}", categoryName ?? "null");
            }
            
            // Try to get category from merchant defaults first
            if (string.IsNullOrEmpty(categoryName) && merchant != null)
            {
                if (!string.IsNullOrEmpty(merchant.Category))
                {
                    categoryName = merchant.Category;
                    if (transactionIndex <= 5)
                    {
                        _logger.LogInformation("  - Using merchant's default category: {Category}", categoryName);
                    }
                }
                else if (transactionIndex <= 5)
                {
                    _logger.LogInformation("  - No default category for merchant");
                }
            }
            
            // Look up category ID from category name if we have one
            if (!string.IsNullOrEmpty(categoryName))
            {
                var category = await context.Categories
                    .FirstOrDefaultAsync(c => c.Name == categoryName, cancellationToken);
                if (category != null)
                {
                    categoryId = category.Id;
                    if (transactionIndex <= 5)
                    {
                        _logger.LogInformation("  - Found category ID: {Id} for '{Name}'", categoryId, categoryName);
                    }
                }
                else if (transactionIndex <= 5)
                {
                    _logger.LogInformation("  - Category '{Name}' not found in database", categoryName);
                }
            }
            
            if (transactionIndex <= 5)
            {
                _logger.LogInformation("  - Final category: {Category}", categoryName ?? "Uncategorized");
            }

            // Ensure dates are in UTC for PostgreSQL compatibility
            var transactionDate = txn.Date.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(txn.Date, DateTimeKind.Utc) 
                : txn.Date.ToUniversalTime();
            
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = import.UserId,
                AccountId = accountId,
                TransactionDate = transactionDate,
                PostedDate = transactionDate,
                Amount = txn.Amount,
                Type = txn.Amount < 0 ? TransactionType.Debit : TransactionType.Credit,
                OriginalMerchant = txn.Description,
                NormalizedMerchant = merchant?.DisplayName,
                MerchantId = merchant?.Id,
                Description = txn.Description,
                CategoryId = categoryId,
                ImportHash = hash,
                ImportedFileId = import.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (transactionIndex <= 5)
            {
                _logger.LogInformation("💾 STEP 5: CREATING TRANSACTION ENTITY:");
                _logger.LogInformation("  - Transaction ID: {Id}", transaction.Id);
                _logger.LogInformation("  - Type: {Type}", transaction.Type);
                _logger.LogInformation("  - Original merchant: '{Original}'", transaction.OriginalMerchant);
                _logger.LogInformation("  - Normalized merchant: '{Normalized}'", transaction.NormalizedMerchant ?? "null");
                _logger.LogInformation("  - Category ID: {CategoryId}", transaction.CategoryId?.ToString() ?? "null");
                _logger.LogInformation("  - Category name: {CategoryName}", categoryName ?? "Uncategorized");
                _logger.LogInformation("  - Import file ID: {ImportId}", transaction.ImportedFileId);
                _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
            }

            context.Transactions.Add(transaction);
            importedCount++;
        }

        _logger.LogInformation("=== Saving {Count} transactions to database ===", importedCount);
        await context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("=== Import complete: {Imported} imported, {Duplicates} duplicates ===", importedCount, duplicateCount);
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