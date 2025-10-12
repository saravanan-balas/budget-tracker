using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services.Merchants;
using BudgetTracker.Common.Services.Categories;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace BudgetTracker.Common.Services.Transactions;

public class OptimizedBatchTransactionService : IBatchTransactionService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IMerchantService _merchantService;
    private readonly ICategoryAssignmentService _categoryService;
    private readonly ILogger<OptimizedBatchTransactionService> _logger;

    // Performance counters
    private long _totalProcessed = 0;
    private long _totalInserted = 0;
    private long _totalDuplicates = 0;
    private long _totalErrors = 0;
    private TimeSpan _totalProcessingTime = TimeSpan.Zero;

    public OptimizedBatchTransactionService(
        BudgetTrackerDbContext context,
        IMerchantService merchantService,
        ICategoryAssignmentService categoryService,
        ILogger<OptimizedBatchTransactionService> logger)
    {
        _context = context;
        _merchantService = merchantService;
        _categoryService = categoryService;
        _logger = logger;
    }

    public async Task<BatchProcessResult> ProcessTransactionBatchAsync(
        List<Transaction> transactions, 
        Guid userId, 
        Guid accountId, 
        Guid importId)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BatchProcessResult();

        try
        {
            _logger.LogInformation("Processing batch of {Count} transactions for user {UserId}", 
                transactions.Count, userId);

            // 1. Calculate hashes for all transactions (for duplicate detection)
            var transactionHashes = transactions.Select(t => GenerateTransactionHash(t)).ToList();
            
            // 2. Find existing duplicates
            var existingHashes = await FindDuplicateHashesAsync(transactionHashes, userId);
            var existingHashSet = existingHashes.ToHashSet();

            // 3. Filter out duplicates
            var newTransactions = new List<Transaction>();
            var duplicateCount = 0;

            for (int i = 0; i < transactions.Count; i++)
            {
                if (existingHashSet.Contains(transactionHashes[i]))
                {
                    duplicateCount++;
                }
                else
                {
                    var transaction = transactions[i];
                    transaction.UserId = userId;
                    transaction.AccountId = accountId;
                    transaction.ImportedFileId = importId;
                    transaction.TransactionHash = transactionHashes[i];
                    newTransactions.Add(transaction);
                }
            }

            if (newTransactions.Count == 0)
            {
                result.TotalProcessed = transactions.Count;
                result.Duplicates = duplicateCount;
                result.ProcessingTime = stopwatch.Elapsed;
                return result;
            }

            // 4. Batch process merchants (normalize and find/create)
            await ProcessMerchantsBatch(newTransactions);

            // 5. Batch assign categories
            await AssignCategoriesBatch(newTransactions, userId);

            // 6. Bulk insert transactions
            var insertedCount = await BulkInsertTransactionsAsync(newTransactions);

            // 7. Update performance counters
            Interlocked.Add(ref _totalProcessed, transactions.Count);
            Interlocked.Add(ref _totalInserted, insertedCount);
            Interlocked.Add(ref _totalDuplicates, duplicateCount);
            _totalProcessingTime = _totalProcessingTime.Add(stopwatch.Elapsed);

            result.TotalProcessed = transactions.Count;
            result.Inserted = insertedCount;
            result.Duplicates = duplicateCount;
            result.ProcessingTime = stopwatch.Elapsed;

            _logger.LogInformation(
                "Batch processed: {Total} total, {Inserted} inserted, {Duplicates} duplicates in {Time}ms",
                transactions.Count, insertedCount, duplicateCount, stopwatch.ElapsedMilliseconds);

        }
        catch (Exception ex)
        {
            Interlocked.Add(ref _totalErrors, transactions.Count);
            result.Errors = transactions.Count;
            result.ErrorMessages.Add(ex.Message);
            _logger.LogError(ex, "Error processing transaction batch");
        }

        stopwatch.Stop();
        result.ProcessingTime = stopwatch.Elapsed;
        return result;
    }

    public async Task<int> BulkInsertTransactionsAsync(List<Transaction> transactions)
    {
        if (transactions.Count == 0) return 0;

        try
        {
            // Set default values for all transactions
            var now = DateTime.UtcNow;
            foreach (var transaction in transactions)
            {
                if (transaction.Id == Guid.Empty)
                    transaction.Id = Guid.NewGuid();
                if (transaction.CreatedAt == DateTime.MinValue)
                    transaction.CreatedAt = now;
                if (transaction.UpdatedAt == DateTime.MinValue)
                    transaction.UpdatedAt = now;
                
                // Ensure all DateTime fields are UTC
                if (transaction.TransactionDate.Kind == DateTimeKind.Unspecified)
                    transaction.TransactionDate = DateTime.SpecifyKind(transaction.TransactionDate, DateTimeKind.Utc);
                if (transaction.PostedDate.Kind == DateTimeKind.Unspecified)
                    transaction.PostedDate = DateTime.SpecifyKind(transaction.PostedDate, DateTimeKind.Utc);
            }

            // Use AddRange for bulk insert (EF Core will batch these)
            await _context.Transactions.AddRangeAsync(transactions);
            
            // Configure batch size for optimal performance
            var savedCount = await _context.SaveChangesAsync();

            return savedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk inserting {Count} transactions", transactions.Count);
            throw;
        }
    }

    public async Task<List<string>> FindDuplicateHashesAsync(List<string> transactionHashes, Guid userId)
    {
        if (transactionHashes.Count == 0) return new List<string>();

        try
        {
            // Query in batches to avoid parameter limits
            const int batchSize = 1000;
            var existingHashes = new List<string>();

            for (int i = 0; i < transactionHashes.Count; i += batchSize)
            {
                var batch = transactionHashes.Skip(i).Take(batchSize).ToList();
                
                var batchExisting = await _context.Transactions
                    .Where(t => t.UserId == userId && batch.Contains(t.TransactionHash))
                    .Select(t => t.TransactionHash)
                    .ToListAsync();

                existingHashes.AddRange(batchExisting);
            }

            return existingHashes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding duplicate hashes for {Count} transactions", transactionHashes.Count);
            return new List<string>();
        }
    }

    private async Task ProcessMerchantsBatch(List<Transaction> transactions)
    {
        // Extract unique merchants
        var uniqueMerchants = transactions
            .Select(t => t.OriginalMerchant)
            .Distinct()
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToList();

        // Batch process merchant normalization
        var merchantMatches = new Dictionary<string, Guid?>();

        foreach (var merchantName in uniqueMerchants)
        {
            try
            {
                var match = await _merchantService.FindBestMatchAsync(merchantName);
                if (match != null)
                {
                    merchantMatches[merchantName] = match.Merchant.Id;
                }
                else
                {
                    // Create new merchant
                    var newMerchant = await _merchantService.CreateOrGetMerchantAsync(merchantName);
                    merchantMatches[merchantName] = newMerchant.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing merchant: {Merchant}", merchantName);
                merchantMatches[merchantName] = null;
            }
        }

        // Apply merchant IDs to transactions
        foreach (var transaction in transactions)
        {
            if (merchantMatches.TryGetValue(transaction.OriginalMerchant, out var merchantId))
            {
                transaction.MerchantId = merchantId;
            }
        }
    }

    private async Task AssignCategoriesBatch(List<Transaction> transactions, Guid userId)
    {
        // Prepare data for batch category assignment
        var transactionData = transactions
            .Select(t => (
                merchant: t.Description ?? t.OriginalMerchant, // Use Description (full name) instead of OriginalMerchant (truncated)
                description: t.Description,
                amount: t.Amount
            ))
            .ToList();

        // Batch assign categories
        _logger.LogInformation("Starting batch categorization for {Count} transactions", transactionData.Count);
        var categoryAssignments = await _categoryService.BatchAssignCategoriesAsync(transactionData, userId);
        _logger.LogInformation("Batch categorization returned {Count} assignments", categoryAssignments.Count);

        // Log the keys from category assignments for debugging
        _logger.LogDebug("Category assignment keys: {Keys}", string.Join(", ", categoryAssignments.Keys.Take(5)));

        // Apply category assignments to transactions
        int categorizedCount = 0;
        for (int i = 0; i < transactions.Count; i++)
        {
            var transaction = transactions[i];
            var merchantForLookup = transaction.Description ?? transaction.OriginalMerchant;
            var lookupKey = $"{merchantForLookup}|{transaction.Description}|{transaction.Amount}";
            
            _logger.LogDebug("Looking for key: '{LookupKey}' for transaction: {Merchant}", lookupKey, transaction.OriginalMerchant);
            
            if (categoryAssignments.TryGetValue(lookupKey, out var categoryId))
            {
                transaction.CategoryId = categoryId;
                if (categoryId.HasValue)
                {
                    categorizedCount++;
                    _logger.LogInformation("Successfully categorized {Merchant} with category ID {CategoryId}", transaction.OriginalMerchant, categoryId);
                }
                else
                {
                    _logger.LogWarning("Category assignment returned null for {Merchant}", transaction.OriginalMerchant);
                }
            }
            else
            {
                _logger.LogWarning("No category assignment found for key: '{LookupKey}' (merchant: {Merchant})", lookupKey, transaction.OriginalMerchant);
            }
        }
        _logger.LogInformation("Applied categories to {Count} out of {Total} transactions", categorizedCount, transactions.Count);
    }

    private string GenerateTransactionHash(Transaction transaction)
    {
        // Create a unique hash based on transaction characteristics
        var hashString = $"{transaction.TransactionDate:yyyy-MM-dd}|{transaction.Amount:F2}|{transaction.OriginalMerchant}|{transaction.Description}";
        
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashString));
        return Convert.ToHexString(hashBytes);
    }

    public async Task<Dictionary<string, object>> GetBatchProcessingStatsAsync()
    {
        var averageProcessingTime = _totalProcessed > 0 
            ? _totalProcessingTime.TotalMilliseconds / _totalProcessed 
            : 0;

        var duplicateRate = _totalProcessed > 0 
            ? (double)_totalDuplicates / _totalProcessed 
            : 0;

        var errorRate = _totalProcessed > 0 
            ? (double)_totalErrors / _totalProcessed 
            : 0;

        return new Dictionary<string, object>
        {
            ["total_processed"] = _totalProcessed,
            ["total_inserted"] = _totalInserted,
            ["total_duplicates"] = _totalDuplicates,
            ["total_errors"] = _totalErrors,
            ["average_processing_time_ms"] = averageProcessingTime,
            ["duplicate_rate"] = duplicateRate,
            ["error_rate"] = errorRate,
            ["total_processing_time_ms"] = _totalProcessingTime.TotalMilliseconds
        };
    }
}