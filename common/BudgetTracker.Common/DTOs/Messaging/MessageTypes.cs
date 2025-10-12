namespace BudgetTracker.Common.DTOs.Messaging;

public class ImportProcessingMessage
{
    public Guid ImportId { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string? DetectedBankName { get; set; }
    public string? DetectedCountry { get; set; }
    public string? DetectedFormat { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public int Priority { get; set; } = 0; // Higher number = higher priority
}

public class RecurringTransactionDetectionMessage
{
    public Guid UserId { get; set; }
    public DateTime DetectionRequestedAt { get; set; } = DateTime.UtcNow;
    public int Priority { get; set; } = 1; // Lower priority than imports
}

 public class MerchantOptimizationMessage
{
    public Guid MerchantId { get; set; }
    public Guid UserId { get; set; }
    public DateTime OptimizationRequestedAt { get; set; } = DateTime.UtcNow;
    public int Priority { get; set; } = 2; // Lower priority
}

public class CategoryOptimizationMessage
{
    public Guid CategoryId { get; set; }
    public Guid UserId { get; set; }
    public DateTime OptimizationRequestedAt { get; set; } = DateTime.UtcNow;
    public int Priority { get; set; } = 2; // Lower priority
}

public class ProcessingJobResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }
}

