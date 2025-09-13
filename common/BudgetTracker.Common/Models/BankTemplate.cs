using System;

namespace BudgetTracker.Common.Models;

public class BankTemplate
{
    public Guid Id { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string FileFormat { get; set; } = string.Empty;
    public string TemplatePattern { get; set; } = string.Empty; // JSON structure
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double ConfidenceScore { get; set; }
    public DateTime LastUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}