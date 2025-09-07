using System;

namespace BudgetTracker.Common.DTOs;

public class AccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Institution { get; set; }
    public string? AccountNumber { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAccountDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal? InitialBalance { get; set; }
    public string? Currency { get; set; }
    public string? Institution { get; set; }
    public string? AccountNumber { get; set; }
}

public class UpdateAccountDto
{
    public string? Name { get; set; }
    public string? Institution { get; set; }
    public string? AccountNumber { get; set; }
    public decimal? Balance { get; set; }
}