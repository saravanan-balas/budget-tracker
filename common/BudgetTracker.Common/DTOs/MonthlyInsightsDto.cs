using System;
using System.Collections.Generic;

namespace BudgetTracker.Common.DTOs;

public class MonthlyInsightsRequestDto
{
    // Month to analyze (1-12)
    public int Month { get; set; }

    // Year to analyze (e.g., 2026)
    public int Year { get; set; }

    // Optional: restrict to specific accounts
    public List<Guid>? AccountIds { get; set; }

    // Optional: number of sample transactions to send to AI (default handled server-side)
    public int? SampleSize { get; set; }
}

public class MonthlyInsightsResponseDto
{
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }

    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal Net { get; set; }
    public int TransactionCount { get; set; }

    public List<ChartDataDto> SpendingByCategory { get; set; } = new();
    public List<MerchantSpendDto> TopMerchants { get; set; } = new();
    public List<SampleTransactionDto> SampleTransactions { get; set; } = new();

    public AiMonthlyInsightsDto Ai { get; set; } = new();
}

public class MerchantSpendDto
{
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; } // absolute amount spent
    public int Count { get; set; }
}

public class SampleTransactionDto
{
    public DateTime TransactionDateUtc { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; } // negative for expenses, positive for income
    public string Category { get; set; } = "Uncategorized";
}

public class AiMonthlyInsightsDto
{
    public bool UsedAi { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public List<string> Watchouts { get; set; } = new();
}

