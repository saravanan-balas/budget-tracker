using System;

namespace BudgetTracker.Common.Models;

public class Rule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RuleType Type { get; set; }
    public string Conditions { get; set; } = string.Empty;
    public string Actions { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public int TimesApplied { get; set; }
    public DateTime? LastApplied { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User User { get; set; } = null!;
    public Category? Category { get; set; }
}

public enum RuleType
{
    Categorization,
    Transfer,
    Split,
    Tag,
    Alert,
    Custom
}

public class RuleCondition
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? LogicalOperator { get; set; }
}

public class RuleAction
{
    public string Type { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? Value { get; set; }
}