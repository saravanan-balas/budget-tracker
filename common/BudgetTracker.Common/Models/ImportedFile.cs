using System;
using System.Collections.Generic;

namespace BudgetTracker.Common.Models;

public class ImportedFile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? BlobUrl { get; set; }
    public ImportStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int ImportedTransactions { get; set; }
    public int DuplicateTransactions { get; set; }
    public int FailedRows { get; set; }
    public string? ErrorDetails { get; set; }
    public string? BankTemplate { get; set; }
    public string? DetectedBankName { get; set; }
    public string? DetectedCountry { get; set; }
    public string? DetectedFormat { get; set; } // CSV, PDF, IMAGE
    public string? TemplateVersion { get; set; }
    public string? ParsingMetadata { get; set; } // JSON field
    public decimal? AICost { get; set; } // Track AI usage cost
    public bool IsProcessedSynchronously { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

public enum ImportStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    PartialSuccess
}