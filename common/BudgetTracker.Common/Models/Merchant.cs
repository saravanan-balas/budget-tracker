using System;
using System.Collections.Generic;
using Pgvector;

namespace BudgetTracker.Common.Models;

public class Merchant
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public Vector? Embedding { get; set; }
    public string? EnrichedData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<RecurringSeries> RecurringSeries { get; set; } = new List<RecurringSeries>();
}