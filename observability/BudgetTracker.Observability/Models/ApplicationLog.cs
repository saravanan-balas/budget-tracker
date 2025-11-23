using System;

namespace BudgetTracker.Observability.Models;

public class ApplicationLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? Source { get; set; }
    public Guid? UserId { get; set; }
    public string? Properties { get; set; }
}

