using BudgetTracker.Common.Models;

namespace BudgetTracker.Common.Services.Transactions;

public interface IBatchTransactionService
{
    /// <summary>
    /// Process multiple transactions in a single optimized operation
    /// Includes deduplication, merchant normalization, and category assignment
    /// </summary>
    Task<BatchProcessResult> ProcessTransactionBatchAsync(
        List<Transaction> transactions, 
        Guid userId, 
        Guid accountId, 
        Guid importId);

    /// <summary>
    /// Bulk insert transactions with optimized database operations
    /// </summary>
    Task<int> BulkInsertTransactionsAsync(List<Transaction> transactions);

    /// <summary>
    /// Check for duplicates across existing transactions
    /// Uses hash-based deduplication for performance
    /// </summary>
    Task<List<string>> FindDuplicateHashesAsync(List<string> transactionHashes, Guid userId);

    /// <summary>
    /// Get batch processing performance statistics
    /// </summary>
    Task<Dictionary<string, object>> GetBatchProcessingStatsAsync();
}

public class BatchProcessResult
{
    public int TotalProcessed { get; set; }
    public int Inserted { get; set; }
    public int Duplicates { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
    public decimal EstimatedAICost { get; set; }
}