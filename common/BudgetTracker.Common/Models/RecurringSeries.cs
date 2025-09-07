using System;
using System.Collections.Generic;

namespace BudgetTracker.Common.Models;

public class RecurringSeries
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? MerchantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public RecurrenceType RecurrenceType { get; set; }
    public int RecurrenceInterval { get; set; } = 1;
    public string? RecurrenceDays { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal AmountTolerance { get; set; } = 0.10m;
    public DateTime? NextExpectedDate { get; set; }
    public DateTime? LastOccurrence { get; set; }
    public bool IsActive { get; set; } = true;
    public SubscriptionType? SubscriptionType { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User User { get; set; } = null!;
    public Merchant? Merchant { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

public enum RecurrenceType
{
    Daily,
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    SemiAnnually,
    Annually,
    Custom
}

public enum SubscriptionType
{
    Entertainment,
    Software,
    Utilities,
    Insurance,
    Membership,
    Other
}