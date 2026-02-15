using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.Merchants;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.Services.Transactions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.API.Services;

public interface ISynchronousImportService
{
    Task<ImportResult> ProcessCsvAsync(Guid userId, FileImportDto importDto);
    Task<ImportStatusDto?> GetImportStatusAsync(Guid userId, Guid importId);
    Task<IEnumerable<ImportStatusDto>> GetImportHistoryAsync(Guid userId);
}

public class SynchronousImportService : ISynchronousImportService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IUniversalBankParser? _universalParser;
    private readonly IAIBankAnalyzer? _aiAnalyzer;
    private readonly IBankTemplateService? _templateService;
    private readonly IBatchTransactionService _batchTransactionService;
    private readonly ILogger<SynchronousImportService> _logger;

    public SynchronousImportService(
        BudgetTrackerDbContext context,
        IBatchTransactionService batchTransactionService,
        ILogger<SynchronousImportService> logger,
        IUniversalBankParser? universalParser = null,
        IAIBankAnalyzer? aiAnalyzer = null,
        IBankTemplateService? templateService = null)
    {
        _context = context;
        _batchTransactionService = batchTransactionService;
        _logger = logger;
        _universalParser = universalParser;
        _aiAnalyzer = aiAnalyzer;
        _templateService = templateService;
    }

    public async Task<ImportResult> ProcessCsvAsync(Guid userId, FileImportDto importDto)
    {
        // Only process CSV files
        if (!importDto.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportResult
            {
                IsSuccessful = false,
                Message = "Only CSV files are supported for synchronous processing"
            };
        }

        _logger.LogInformation("📁 Starting synchronous CSV processing for {FileName} ({FileSize} bytes)", 
            importDto.FileName, importDto.FileData.Length);

        try
        {
            // Create import record
            var importFile = await CreateImportRecordAsync(userId, importDto);
            
            // Process the CSV file immediately
            var transactions = await ParseCsvFileAsync(importFile, importDto.FileData);
            
            _logger.LogInformation("📊 Found {Count} transactions in CSV file", transactions.Count);
            
            // Get or create account for import
            var account = await GetOrCreateAccountForImportAsync(userId, importDto.AccountId);
            
            // Import transactions
            importFile.TotalRows = transactions.Count;
            var (importedCount, duplicateCount) = await ImportTransactionsAsync(
                importFile, transactions, userId, account.Id);

            // Update import record with results
            importFile.Status = ImportStatus.Completed;
            importFile.ImportedTransactions = importedCount;
            importFile.DuplicateTransactions = duplicateCount;
            importFile.ProcessedRows = transactions.Count;
            importFile.ProcessingCompletedAt = DateTime.UtcNow;
            importFile.IsProcessedSynchronously = true;
            importFile.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ CSV import completed: {Imported} imported, {Duplicates} duplicates", 
                importedCount, duplicateCount);

            return new ImportResult
            {
                ImportId = importFile.Id,
                IsSuccessful = true,
                IsAsync = false,
                Message = $"Successfully imported {importedCount} transactions ({duplicateCount} duplicates skipped)",
                EstimatedSeconds = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing CSV file: {Error}", ex.Message);
            return new ImportResult
            {
                IsSuccessful = false,
                Message = $"Processing failed: {ex.Message}"
            };
        }
    }

    public async Task<ImportStatusDto?> GetImportStatusAsync(Guid userId, Guid importId)
    {
        var importFile = await _context.ImportedFiles
            .FirstOrDefaultAsync(f => f.Id == importId && f.UserId == userId);

        if (importFile == null)
            return null;

        return new ImportStatusDto
        {
            ImportId = importFile.Id,
            Status = importFile.Status.ToString(),
            TotalRows = importFile.TotalRows,
            ProcessedRows = importFile.ProcessedRows,
            ImportedTransactions = importFile.ImportedTransactions,
            DuplicateTransactions = importFile.DuplicateTransactions,
            FailedRows = importFile.FailedRows,
            ErrorDetails = importFile.ErrorDetails,
            DetectedBankName = importFile.DetectedBankName,
            DetectedFormat = importFile.DetectedFormat,
            AICost = importFile.AICost,
            IsProcessedSynchronously = importFile.IsProcessedSynchronously
        };
    }

    public async Task<IEnumerable<ImportStatusDto>> GetImportHistoryAsync(Guid userId)
    {
        return await _context.ImportedFiles
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(20)
            .Select(f => new ImportStatusDto
            {
                ImportId = f.Id,
                Status = f.Status.ToString(),
                TotalRows = f.TotalRows,
                ProcessedRows = f.ProcessedRows,
                ImportedTransactions = f.ImportedTransactions,
                DuplicateTransactions = f.DuplicateTransactions,
                FailedRows = f.FailedRows,
                ErrorDetails = f.ErrorDetails,
                DetectedBankName = f.DetectedBankName,
                DetectedFormat = f.DetectedFormat,
                AICost = f.AICost,
                IsProcessedSynchronously = f.IsProcessedSynchronously
            })
            .ToListAsync();
    }

    private async Task<List<ParsedTransaction>> ParseCsvFileAsync(ImportedFile import, byte[] fileData)
    {
        // Use universal parser if available
        if (_universalParser != null)
        {
            try
            {
                _logger.LogInformation("🔍 Using universal parser for CSV file");
                
                // Detect bank if AI analyzer is available
                BankDetectionResult? bankInfo = null;
                if (_aiAnalyzer != null)
                {
                    _logger.LogInformation("🏦 Detecting bank from CSV content...");
                    bankInfo = await _aiAnalyzer.DetectBankAsync(fileData, import.FileName);
                    
                    if (bankInfo != null)
                    {
                        _logger.LogInformation("🏦 Detected: {Bank} ({Country})", bankInfo.BankName, bankInfo.Country);
                        import.DetectedBankName = bankInfo.BankName;
                        import.DetectedCountry = bankInfo.Country;
                        import.DetectedFormat = "csv";
                    }
                }

                var parseResult = await _universalParser.ParseFileAsync(fileData, import.FileName, bankInfo);
                
                if (parseResult.IsSuccessful)
                {
                    _logger.LogInformation("✅ Parsed {Count} transactions (AI Cost: ${Cost:F4})", 
                        parseResult.Transactions.Count, parseResult.AICost);
                    
                    import.AICost = parseResult.AICost;
                    
                    // Save template if successful
                    if (bankInfo != null && _templateService != null)
                    {
                        try
                        {
                            await _templateService.SaveTemplateAsync(bankInfo, parseResult);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to save template");
                        }
                    }
                    
                    return parseResult.Transactions;
                }
                else
                {
                    _logger.LogWarning("Parser failed: {Error}", parseResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error using universal parser");
            }
        }

        // Fallback to simple CSV parsing
        _logger.LogInformation("⚠️ Using simple CSV parser");
        return ParseCsvSimple(fileData);
    }

    private List<ParsedTransaction> ParseCsvSimple(byte[] fileData)
    {
        var transactions = new List<ParsedTransaction>();
        var csvText = Encoding.UTF8.GetString(fileData);
        var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Skip header if present
        var startIndex = 1;
        if (lines.Length > 0 && lines[0].Contains("Date", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }

        for (int i = startIndex; i < lines.Length; i++)
        {
            try
            {
                var parts = lines[i].Split(',');
                if (parts.Length >= 3)
                {
                    var dateStr = parts[0].Trim('"');
                    var description = parts[1].Trim('"');
                    var amountStr = parts[2].Trim('"').Replace("$", "");

                    if (DateTime.TryParse(dateStr, out var date) && 
                        decimal.TryParse(amountStr, out var amount))
                    {
                        transactions.Add(new ParsedTransaction
                        {
                            Date = date,
                            Description = description,
                            Amount = amount,
                            Category = parts.Length > 3 ? parts[3].Trim('"') : null
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse line {LineNumber}: {Error}", i, ex.Message);
            }
        }

        return transactions;
    }

    private async Task<ImportedFile> CreateImportRecordAsync(Guid userId, FileImportDto importDto)
    {
        var importFile = new ImportedFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = importDto.FileName,
            FileType = importDto.FileType,
            FileSize = importDto.FileData.Length,
            Status = ImportStatus.Processing,
            ProcessingStartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ImportedFiles.Add(importFile);
        await _context.SaveChangesAsync();

        return importFile;
    }

    private async Task<Account> GetOrCreateAccountForImportAsync(Guid userId, Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

        if (account == null)
        {
            // Try to get any account for the user
            account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId);
            
            if (account == null)
            {
                // Create a default account if none exists
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = "Default Account",
                    Type = AccountType.Checking,
                    Balance = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created default account for user {UserId}", userId);
            }
        }

        return account;
    }

    private async Task<(int imported, int duplicates)> ImportTransactionsAsync(
        ImportedFile import, 
        List<ParsedTransaction> transactions, 
        Guid userId,
        Guid accountId)
    {
        _logger.LogInformation("🔄 Processing {Count} transactions with batch service", transactions.Count);

        // Convert ParsedTransactions to Transaction objects
        var transactionList = transactions.Select(parsedTxn => new Transaction
        {
            TransactionDate = parsedTxn.Date,
            PostedDate = parsedTxn.Date,
            Amount = parsedTxn.Amount,
            Type = parsedTxn.Amount >= 0 ? TransactionType.Credit : TransactionType.Debit,
            Description = parsedTxn.Description ?? "Import",
            OriginalMerchant = ExtractMerchantFromDescription(parsedTxn.Description),
            ImportedFileId = import.Id
        }).ToList();

        // Process batch
        var result = await _batchTransactionService.ProcessTransactionBatchAsync(
            transactionList,
            userId,
            accountId,
            import.Id);

        _logger.LogInformation("📊 Batch processing complete: {Inserted} inserted, {Duplicates} duplicates",
            result.Inserted, result.Duplicates);

        return (result.Inserted, result.Duplicates);
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
        
        // Split by delimiters that typically separate merchant from ref/ID (keep # and * as split points)
        var parts = desc.Split(new[] { ' ', '#', '*' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Unknown";

        // Take tokens until we hit: all-digits, obvious ref/ID, or max tokens
        var merchantTokens = new List<string>();
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REF", "PAYMENT", "AUTH", "PENDING", "ONLINE", "CARD" };
        const int maxTokens = 4;

        foreach (var part in parts)
        {
            if (merchantTokens.Count >= maxTokens) break;
            var p = part.Trim('-');
            if (string.IsNullOrEmpty(p)) continue;
            if (p.All(char.IsDigit) && p.Length >= 4) break;  // Likely store/ref number
            if (stopWords.Contains(p)) break;
            merchantTokens.Add(p);
        }

        return merchantTokens.Count > 0 ? string.Join(" ", merchantTokens) : parts[0];
    }
}