using System;

namespace BudgetTracker.Common.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public DateTime PostedDate { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public string? NormalizedMerchant { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsPending { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsTransfer { get; set; }
    public bool IsSplit { get; set; }
    public string? Tags { get; set; }
}

public class CreateTransactionDto
{
    public Guid AccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }
}

public class UpdateTransactionDto
{
    public Guid? CategoryId { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }
    public string? Merchant { get; set; }
}

public class SplitTransactionDto
{
    public Guid TransactionId { get; set; }
    public List<SplitItemDto> Splits { get; set; } = new();
}

public class SplitItemDto
{
    public decimal Amount { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
}

public class TransactionFilterDto
{
    public Guid? AccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? SearchTerm { get; set; }
    public bool? IsRecurring { get; set; }
    public bool? IsPending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}