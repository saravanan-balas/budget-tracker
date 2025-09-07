using System;
using System.Collections.Generic;

namespace BudgetTracker.Common.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public string Currency { get; set; } = "USD";
    public string Country { get; set; } = "US";
    public string TimeZone { get; set; } = "UTC";
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
    public DateTime? SubscriptionExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Rule> Rules { get; set; } = new List<Rule>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public ICollection<ImportedFile> ImportedFiles { get; set; } = new List<ImportedFile>();
}

public enum SubscriptionTier
{
    Free,
    Pro,
    Family,
    Business
}