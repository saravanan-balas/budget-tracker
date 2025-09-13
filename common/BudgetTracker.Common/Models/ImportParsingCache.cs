using System;

namespace BudgetTracker.Common.Models;

public class ImportParsingCache
{
    public Guid Id { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string ParsedStructure { get; set; } = string.Empty; // JSON
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}