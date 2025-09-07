using System;
using Pgvector;

namespace BudgetTracker.Common.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime PostedDate { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string OriginalMerchant { get; set; } = string.Empty;
    public string? NormalizedMerchant { get; set; }
    public Guid? MerchantId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsPending { get; set; }
    public bool IsRecurring { get; set; }
    public Guid? RecurringSeriesId { get; set; }
    public bool IsTransfer { get; set; }
    public Guid? TransferPairId { get; set; }
    public bool IsSplit { get; set; }
    public Guid? ParentTransactionId { get; set; }
    public string? Tags { get; set; }
    public string? ImportHash { get; set; }
    public Guid? ImportedFileId { get; set; }
    // public Vector? Embedding { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User User { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public Merchant? Merchant { get; set; }
    public Category? Category { get; set; }
    public RecurringSeries? RecurringSeries { get; set; }
    public Transaction? TransferPair { get; set; }
    public Transaction? ParentTransaction { get; set; }
    public ImportedFile? ImportedFile { get; set; }
    public ICollection<Transaction> SplitTransactions { get; set; } = new List<Transaction>();
}

public enum TransactionType
{
    Debit,
    Credit,
    Transfer
}