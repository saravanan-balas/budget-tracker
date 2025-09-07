using System;

namespace BudgetTracker.Common.Models;

public class Goal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GoalType Type { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime TargetDate { get; set; }
    public string? CategoryScope { get; set; }
    public string? AccountScope { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User User { get; set; } = null!;
}

public enum GoalType
{
    Savings,
    DebtReduction,
    SpendingLimit,
    Investment,
    Emergency,
    Custom
}

public enum GoalStatus
{
    Active,
    Paused,
    Achieved,
    Failed,
    Cancelled
}