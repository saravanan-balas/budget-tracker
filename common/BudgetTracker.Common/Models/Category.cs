using System;
using System.Collections.Generic;

namespace BudgetTracker.Common.Models;

public class Category
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public CategoryType Type { get; set; }
    public decimal? BudgetAmount { get; set; }
    public BudgetPeriod? BudgetPeriod { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User User { get; set; } = null!;
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Rule> Rules { get; set; } = new List<Rule>();
}

public enum CategoryType
{
    Income,
    Expense,
    Transfer,
    Savings
}

public enum BudgetPeriod
{
    Daily,
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    Yearly
}