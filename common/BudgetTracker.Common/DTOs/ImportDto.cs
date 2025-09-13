using System;

namespace BudgetTracker.Common.DTOs;

public class FileImportDto
{
    public Guid AccountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public byte[] FileData { get; set; } = Array.Empty<byte>();
    public string? BankTemplate { get; set; }
}

public class ImportStatusDto
{
    public Guid ImportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int ImportedTransactions { get; set; }
    public int DuplicateTransactions { get; set; }
    public int FailedRows { get; set; }
    public string? ErrorDetails { get; set; }
    public string? DetectedBankName { get; set; }
    public string? DetectedFormat { get; set; }
    public decimal? AICost { get; set; }
    public bool IsProcessedSynchronously { get; set; }
    public int? EstimatedSecondsRemaining { get; set; }
}

public class ImportPreviewDto
{
    public List<string> Headers { get; set; } = new();
    public List<Dictionary<string, string>> SampleRows { get; set; } = new();
    public ColumnMappingDto SuggestedMapping { get; set; } = new();
}

public class ColumnMappingDto
{
    public int? DateColumn { get; set; }
    public int? AmountColumn { get; set; }
    public int? MerchantColumn { get; set; }
    public int? DescriptionColumn { get; set; }
    public int? CategoryColumn { get; set; }
    public string? DateFormat { get; set; }
}

public class BankDetectionResult
{
    public string BankName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string FileFormat { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? TemplateId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class TransactionParsingResult
{
    public List<ParsedTransaction> Transactions { get; set; } = new();
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal AICost { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

public class ParsedTransaction
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Category { get; set; }
    public string? Reference { get; set; }
}

public class FileAnalysisResult
{
    public string FileFormat { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool CanProcessSynchronously { get; set; }
    public string AsyncReason { get; set; } = string.Empty;
    public int EstimatedSeconds { get; set; }
    public bool HasKnownTemplate { get; set; }
    public int EstimatedRowCount { get; set; }
}

public class ImportResult
{
    public Guid? ImportId { get; set; }
    public Guid? JobId { get; set; }
    public bool IsAsync { get; set; }
    public bool IsSuccessful { get; set; }
    public string? Message { get; set; }
    public int EstimatedSeconds { get; set; }
    public List<ParsedTransaction>? Transactions { get; set; }
}