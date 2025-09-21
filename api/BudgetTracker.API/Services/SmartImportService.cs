using System.Diagnostics;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.Services.Merchants;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.API.Services;

public class SmartImportService : ISmartImportService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IFormatDetectionService _formatDetection;
    private readonly IUniversalBankParser _universalParser;
    private readonly IAIBankAnalyzer _aiAnalyzer;
    private readonly IOCRService _ocrService;
    private readonly IBankTemplateService _templateService;
    private readonly IMerchantService _merchantService;
    private readonly ILogger<SmartImportService> _logger;

    public SmartImportService(
        BudgetTrackerDbContext context,
        IBlobStorageService blobStorageService,
        IFormatDetectionService formatDetection,
        IUniversalBankParser universalParser,
        IAIBankAnalyzer aiAnalyzer,
        IOCRService ocrService,
        IBankTemplateService templateService,
        IMerchantService merchantService,
        ILogger<SmartImportService> logger)
    {
        _context = context;
        _blobStorageService = blobStorageService;
        _formatDetection = formatDetection;
        _universalParser = universalParser;
        _aiAnalyzer = aiAnalyzer;
        _ocrService = ocrService;
        _templateService = templateService;
        _merchantService = merchantService;
        _logger = logger;
    }

    public async Task<ImportResult> ProcessSmartImportAsync(Guid userId, FileImportDto importDto)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting smart import processing for user {UserId}, file {FileName}", 
            userId, importDto.FileName);

        try
        {
            // Step 1: Analyze file to determine processing approach
            var analysis = await AnalyzeImportFileAsync(importDto.FileData, importDto.FileName);

            // Step 2: Create import record
            var importFile = await CreateImportRecordAsync(userId, importDto, analysis);

            // Step 3: Decide between sync and async processing
            if (analysis.CanProcessSynchronously)
            {
                _logger.LogInformation("Processing file {FileName} synchronously", importDto.FileName);
                
                // Process immediately and return results
                var result = await ProcessSynchronouslyAsync(importFile, importDto, analysis);
                stopwatch.Stop();
                
                _logger.LogInformation("Synchronous processing completed in {Duration}ms for {FileName}", 
                    stopwatch.ElapsedMilliseconds, importDto.FileName);
                
                return result;
            }
            else
            {
                _logger.LogInformation("Queueing file {FileName} for asynchronous processing: {Reason}", 
                    importDto.FileName, analysis.AsyncReason);
                
                // Queue for background processing
                var jobId = await QueueForAsyncProcessingAsync(importFile, importDto);
                
                return new ImportResult
                {
                    JobId = jobId,
                    ImportId = importFile.Id,
                    IsAsync = true,
                    IsSuccessful = true,
                    Message = $"File queued for processing. {analysis.AsyncReason}",
                    EstimatedSeconds = analysis.EstimatedSeconds
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in smart import processing for file {FileName}", importDto.FileName);
            return new ImportResult
            {
                IsAsync = false,
                IsSuccessful = false,
                Message = $"Import failed: {ex.Message}"
            };
        }
    }

    public async Task<FileAnalysisResult> AnalyzeImportFileAsync(byte[] fileData, string fileName)
    {
        _logger.LogInformation("Analyzing import file: {FileName}", fileName);

        // Get basic file analysis
        var analysis = await _formatDetection.AnalyzeFileAsync(fileData, fileName);

        // Check if we have a known template for this type of file
        if (analysis.FileFormat == "CSV")
        {
            // For CSV files, try to detect bank from content
            try
            {
                var bankInfo = await _aiAnalyzer.DetectBankAsync(fileData, fileName);
                var template = await _templateService.FindTemplateAsync(
                    bankInfo.BankName, bankInfo.Country, bankInfo.FileFormat);

                if (template != null && template.ConfidenceScore > 0.7)
                {
                    analysis.HasKnownTemplate = true;
                    _logger.LogInformation("Found reliable template for {BankName} (confidence: {Confidence})", 
                        bankInfo.BankName, template.ConfidenceScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting bank for CSV file, will use default processing");
            }
        }

        return analysis;
    }

    public async Task<ImportPreviewDto> GeneratePreviewAsync(byte[] fileData, string fileName)
    {
        _logger.LogInformation("Generating preview for file: {FileName}", fileName);

        try
        {
            var format = await _formatDetection.DetectFormatAsync(fileData, fileName);

            return format.ToUpper() switch
            {
                "CSV" => await _universalParser.GeneratePreviewAsync(fileData, fileName),
                "PDF" => await GeneratePdfPreviewAsync(fileData, fileName),
                "PNG" or "JPEG" => await GenerateImagePreviewAsync(fileData, fileName),
                _ => new ImportPreviewDto()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating preview for file {FileName}", fileName);
            return new ImportPreviewDto();
        }
    }

    public async Task<decimal> EstimateProcessingCostAsync(byte[] fileData, string fileName)
    {
        try
        {
            var format = await _formatDetection.DetectFormatAsync(fileData, fileName);
            var needsAI = await _aiAnalyzer.IsAIProcessingRequired(fileData, fileName);

            if (!needsAI)
            {
                return 0m; // No AI cost for template-based processing
            }

            return await _aiAnalyzer.EstimateAICostAsync(fileData.Length, format);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error estimating cost, returning default estimate");
            return 0.05m; // Default estimate
        }
    }

    private async Task<ImportedFile> CreateImportRecordAsync(Guid userId, FileImportDto importDto, FileAnalysisResult analysis)
    {
        var importFile = new ImportedFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = importDto.FileName,
            FileType = importDto.FileType,
            FileSize = importDto.FileData.Length,
            Status = ImportStatus.Pending,
            BankTemplate = importDto.BankTemplate,
            DetectedFormat = analysis.FileFormat,
            IsProcessedSynchronously = analysis.CanProcessSynchronously,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ImportedFiles.Add(importFile);
        await _context.SaveChangesAsync();

        // Upload file to blob storage
        var blobUrl = await _blobStorageService.UploadFileAsync(
            "imports",
            $"{userId}/{importFile.Id}{importDto.FileType}",
            new MemoryStream(importDto.FileData),
            "application/octet-stream"
        );

        importFile.BlobUrl = blobUrl;
        await _context.SaveChangesAsync();

        return importFile;
    }

    private async Task<ImportResult> ProcessSynchronouslyAsync(
        ImportedFile importFile, 
        FileImportDto importDto, 
        FileAnalysisResult analysis)
    {
        try
        {
            importFile.Status = ImportStatus.Processing;
            importFile.ProcessingStartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Parse transactions
            var parseResult = await _universalParser.ParseFileAsync(importDto.FileData, importDto.FileName);

            if (!parseResult.IsSuccessful)
            {
                throw new InvalidOperationException(parseResult.ErrorMessage ?? "Parsing failed");
            }

            // Get or create account
            var account = await GetOrCreateAccountAsync(importFile.UserId, importDto.AccountId);

            // Ensure user has default categories
            await EnsureDefaultCategoriesAsync(importFile.UserId);

            // Import transactions
            var importCount = await ImportTransactionsAsync(importFile, parseResult.Transactions, account.Id);

            // Update import status
            importFile.Status = ImportStatus.Completed;
            importFile.ProcessingCompletedAt = DateTime.UtcNow;
            importFile.TotalRows = parseResult.Transactions.Count;
            importFile.ProcessedRows = parseResult.Transactions.Count;
            importFile.ImportedTransactions = importCount.imported;
            importFile.DuplicateTransactions = importCount.duplicates;
            importFile.AICost = parseResult.AICost;
            importFile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ImportResult
            {
                ImportId = importFile.Id,
                IsAsync = false,
                IsSuccessful = true,
                Message = $"Successfully imported {importCount.imported} transactions ({importCount.duplicates} duplicates skipped)",
                Transactions = parseResult.Transactions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in synchronous processing for import {ImportId}", importFile.Id);
            
            importFile.Status = ImportStatus.Failed;
            importFile.ErrorDetails = ex.Message;
            importFile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            throw;
        }
    }

    private async Task<Guid> QueueForAsyncProcessingAsync(ImportedFile importFile, FileImportDto importDto)
    {
        // Mark as processing - the worker will pick it up
        importFile.Status = ImportStatus.Processing;
        importFile.ProcessingStartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Queued import {ImportId} for background processing", importFile.Id);
        
        return importFile.Id; // Using import ID as job ID for simplicity
    }

    private async Task<Account> GetOrCreateAccountAsync(Guid userId, Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

        if (account == null)
        {
            throw new ArgumentException($"Account {accountId} not found or not accessible to this user. Please ensure you're using a valid account ID.", nameof(accountId));
        }

        return account;
    }

    private async Task EnsureDefaultCategoriesAsync(Guid userId)
    {
        // Check if user already has categories
        var hasCategories = await _context.Categories.AnyAsync(c => c.UserId == userId);
        
        if (hasCategories)
        {
            return; // User already has categories
        }

        _logger.LogInformation("Creating default categories for user {UserId}", userId);

        var defaultCategories = new[]
        {
            // Income categories
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Salary", Type = CategoryType.Income, Icon = "💰", Color = "#10b981", DisplayOrder = 1 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Freelance", Type = CategoryType.Income, Icon = "💼", Color = "#10b981", DisplayOrder = 2 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Investments", Type = CategoryType.Income, Icon = "📈", Color = "#10b981", DisplayOrder = 3 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Other Income", Type = CategoryType.Income, Icon = "💵", Color = "#10b981", DisplayOrder = 4 },
            
            // Expense categories
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Food & Dining", Type = CategoryType.Expense, Icon = "🍔", Color = "#ef4444", DisplayOrder = 10 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Groceries", Type = CategoryType.Expense, Icon = "🛒", Color = "#f97316", DisplayOrder = 11 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Transportation", Type = CategoryType.Expense, Icon = "🚗", Color = "#eab308", DisplayOrder = 12 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Shopping", Type = CategoryType.Expense, Icon = "🛍️", Color = "#a855f7", DisplayOrder = 13 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Entertainment", Type = CategoryType.Expense, Icon = "🎬", Color = "#8b5cf6", DisplayOrder = 14 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Bills & Utilities", Type = CategoryType.Expense, Icon = "📱", Color = "#3b82f6", DisplayOrder = 15 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Healthcare", Type = CategoryType.Expense, Icon = "🏥", Color = "#06b6d4", DisplayOrder = 16 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Education", Type = CategoryType.Expense, Icon = "📚", Color = "#14b8a6", DisplayOrder = 17 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Travel", Type = CategoryType.Expense, Icon = "✈️", Color = "#ec4899", DisplayOrder = 18 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Insurance", Type = CategoryType.Expense, Icon = "🛡️", Color = "#6366f1", DisplayOrder = 19 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Rent/Mortgage", Type = CategoryType.Expense, Icon = "🏠", Color = "#f43f5e", DisplayOrder = 20 },
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Personal Care", Type = CategoryType.Expense, Icon = "💅", Color = "#fb923c", DisplayOrder = 21 },
            
            // Transfer category
            new Category { Id = Guid.NewGuid(), UserId = userId, Name = "Transfer", Type = CategoryType.Transfer, Icon = "↔️", Color = "#6b7280", DisplayOrder = 30 }
        };

        var utcNow = DateTime.UtcNow;
        foreach (var category in defaultCategories)
        {
            category.IsSystem = false;
            category.IsActive = true;
            category.CreatedAt = utcNow;
            category.UpdatedAt = utcNow;
        }

        _context.Categories.AddRange(defaultCategories);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created {Count} default categories for user {UserId}", defaultCategories.Length, userId);
    }

    private async Task<(int imported, int duplicates)> ImportTransactionsAsync(
        ImportedFile importFile, 
        List<ParsedTransaction> transactions, 
        Guid accountId)
    {
        int imported = 0;
        int duplicates = 0;

        _logger.LogInformation("Processing {Count} transactions for import", transactions.Count);

        for (int i = 0; i < transactions.Count; i++)
        {
            var txn = transactions[i];
            var hash = GenerateTransactionHash(txn, accountId);
            
            var exists = await _context.Transactions
                .AnyAsync(t => t.ImportHash == hash && t.UserId == importFile.UserId);

            if (exists)
            {
                duplicates++;
                continue;
            }

            // Merchant normalization
            Merchant? merchant = null;
            if (!string.IsNullOrWhiteSpace(txn.Description))
            {
                try
                {
                    var matchResult = await _merchantService.FindBestMatchAsync(txn.Description, 0.7);
                    if (matchResult != null)
                    {
                        merchant = matchResult.Merchant;
                        if (i < 5) // Log first 5 for debugging
                        {
                            _logger.LogInformation("Matched merchant '{Original}' to '{Normalized}' (method: {Method}, score: {Score:F3})",
                                txn.Description, merchant.DisplayName, matchResult.MatchMethod, matchResult.SimilarityScore);
                        }
                    }
                    else
                    {
                        // Create new merchant if no good match found
                        merchant = await _merchantService.CreateOrGetMerchantAsync(txn.Description, txn.Category);
                        if (i < 5) // Log first 5 for debugging
                        {
                            _logger.LogInformation("Created new merchant '{Name}' for '{Original}'", 
                                merchant.DisplayName, txn.Description);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing merchant for transaction: {Description}", txn.Description);
                }
            }

            // Categorization
            string? categoryName = txn.Category;
            Guid? categoryId = null;
            
            // Try to get category from merchant defaults first
            if (string.IsNullOrEmpty(categoryName) && merchant != null)
            {
                categoryName = merchant.Category;
            }
            
            // Look up category ID from category name if we have one
            if (!string.IsNullOrEmpty(categoryName))
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower() && c.UserId == importFile.UserId);
                
                if (category != null)
                {
                    categoryId = category.Id;
                    if (i < 5) // Log first 5 for debugging
                    {
                        _logger.LogInformation("Categorized transaction as '{Category}'", category.Name);
                    }
                }
                else if (i < 5)
                {
                    _logger.LogWarning("Category '{CategoryName}' not found for user", categoryName);
                }
            }

            // Ensure DateTime values are UTC
            var transactionDate = txn.Date.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(txn.Date, DateTimeKind.Utc) 
                : txn.Date.ToUniversalTime();

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = importFile.UserId,
                AccountId = accountId,
                TransactionDate = transactionDate,
                PostedDate = transactionDate,
                Amount = txn.Amount,
                Type = txn.Amount < 0 ? TransactionType.Debit : TransactionType.Credit,
                OriginalMerchant = txn.Description,
                NormalizedMerchant = merchant?.DisplayName ?? txn.Description,
                MerchantId = merchant?.Id,
                Description = txn.Description,
                CategoryId = categoryId,
                ImportHash = hash,
                ImportedFileId = importFile.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            imported++;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Import completed: {Imported} imported, {Duplicates} duplicates", imported, duplicates);
        
        return (imported, duplicates);
    }

    private string GenerateTransactionHash(ParsedTransaction txn, Guid accountId)
    {
        var input = $"{accountId}|{txn.Date:yyyy-MM-dd}|{txn.Amount:F2}|{txn.Description}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private async Task<ImportPreviewDto> GeneratePdfPreviewAsync(byte[] fileData, string fileName)
    {
        // Extract text using OCR first
        var ocrResult = await _ocrService.ExtractTextWithConfidenceAsync(fileData, fileName);
        
        if (!ocrResult.IsSuccessful)
        {
            return new ImportPreviewDto
            {
                Headers = new List<string> { "Error: Unable to extract text from PDF" }
            };
        }

        return new ImportPreviewDto
        {
            Headers = new List<string> { "Extracted Text Preview" },
            SampleRows = new List<Dictionary<string, string>>
            {
                new() { ["Content"] = ocrResult.ExtractedText.Take(500) + "..." }
            }
        };
    }

    private async Task<ImportPreviewDto> GenerateImagePreviewAsync(byte[] fileData, string fileName)
    {
        // Extract text using OCR
        var ocrResult = await _ocrService.ExtractTextWithConfidenceAsync(fileData, fileName);
        
        if (!ocrResult.IsSuccessful)
        {
            return new ImportPreviewDto
            {
                Headers = new List<string> { "Error: Unable to extract text from image" }
            };
        }

        return new ImportPreviewDto
        {
            Headers = new List<string> { $"OCR Extracted Text (Confidence: {ocrResult.OverallConfidence:P})" },
            SampleRows = new List<Dictionary<string, string>>
            {
                new() { ["Content"] = ocrResult.ExtractedText }
            }
        };
    }
}