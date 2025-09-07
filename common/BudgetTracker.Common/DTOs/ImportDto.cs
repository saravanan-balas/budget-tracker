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