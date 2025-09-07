using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.API.Services;

public class ChatService : IChatService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly ILogger<ChatService> _logger;

    public ChatService(BudgetTrackerDbContext context, ILogger<ChatService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatResponseDto> ProcessChatAsync(Guid userId, ChatRequestDto request)
    {
        _logger.LogInformation("Processing chat request for user {UserId}: {Message}", userId, request.Message);

        var message = request.Message.ToLower();

        if (message.Contains("spend") || message.Contains("spent"))
        {
            return await GetSpendingAnalysisAsync(userId, request);
        }
        
        if (message.Contains("budget") || message.Contains("save"))
        {
            return await GetBudgetAnalysisAsync(userId, request);
        }

        if (message.Contains("recurring") || message.Contains("subscription"))
        {
            return await GetRecurringAnalysisAsync(userId, request);
        }

        return new ChatResponseDto
        {
            Type = ChatResponseType.Text,
            Message = "I can help you analyze your spending, track budgets, and identify recurring transactions. Try asking me about your spending in specific categories or time periods!",
            Suggestions = new List<string>
            {
                "How much did I spend on groceries last month?",
                "Show me my top spending categories",
                "What are my recurring subscriptions?",
                "Compare my spending this month vs last month"
            }
        };
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(Guid userId)
    {
        var hasTransactions = await _context.Transactions
            .AnyAsync(t => t.UserId == userId);

        if (!hasTransactions)
        {
            return new List<string>
            {
                "Import your first bank statement",
                "Set up spending categories",
                "Create a budget"
            };
        }

        var suggestions = new List<string>
        {
            "How much did I spend last month?",
            "What are my top spending categories?",
            "Show me unusual transactions",
            "What subscriptions am I paying for?",
            "How much can I save this month?",
            "Compare my spending to previous months"
        };

        return suggestions;
    }

    private async Task<ChatResponseDto> GetSpendingAnalysisAsync(Guid userId, ChatRequestDto request)
    {
        var startDate = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var endDate = request.EndDate ?? DateTime.UtcNow;

        var spending = await _context.Transactions
            .Where(t => t.UserId == userId && 
                       t.TransactionDate >= startDate && 
                       t.TransactionDate <= endDate &&
                       t.Amount < 0)
            .GroupBy(t => t.Category!.Name)
            .Select(g => new ChartDataDto
            {
                Label = g.Key ?? "Uncategorized",
                Value = Math.Abs(g.Sum(t => t.Amount))
            })
            .OrderByDescending(c => c.Value)
            .Take(10)
            .ToListAsync();

        var total = spending.Sum(s => s.Value);

        return new ChatResponseDto
        {
            Type = ChatResponseType.Chart,
            Message = $"You spent ${total:N2} between {startDate:MMM dd} and {endDate:MMM dd}. Here's the breakdown by category:",
            ChartData = spending
        };
    }

    private async Task<ChatResponseDto> GetBudgetAnalysisAsync(Guid userId, ChatRequestDto request)
    {
        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        
        var monthlySpending = await _context.Transactions
            .Where(t => t.UserId == userId && 
                       t.TransactionDate >= currentMonth &&
                       t.Amount < 0)
            .SumAsync(t => Math.Abs(t.Amount));

        var monthlyIncome = await _context.Transactions
            .Where(t => t.UserId == userId && 
                       t.TransactionDate >= currentMonth &&
                       t.Amount > 0)
            .SumAsync(t => t.Amount);

        var netSavings = monthlyIncome - monthlySpending;

        return new ChatResponseDto
        {
            Type = ChatResponseType.Metric,
            Message = $"This month: Income ${monthlyIncome:N2}, Expenses ${monthlySpending:N2}, Net {(netSavings >= 0 ? "Savings" : "Deficit")} ${Math.Abs(netSavings):N2}",
            Data = new MetricDto
            {
                Name = "Monthly Summary",
                Value = netSavings,
                Unit = "USD",
                Period = "This Month"
            }
        };
    }

    private async Task<ChatResponseDto> GetRecurringAnalysisAsync(Guid userId, ChatRequestDto request)
    {
        var recurringTransactions = await _context.Transactions
            .Where(t => t.UserId == userId && t.IsRecurring)
            .GroupBy(t => t.NormalizedMerchant ?? t.OriginalMerchant)
            .Select(g => new
            {
                Merchant = g.Key,
                Amount = g.Average(t => Math.Abs(t.Amount)),
                Count = g.Count()
            })
            .OrderByDescending(r => r.Amount)
            .Take(10)
            .ToListAsync();

        var chartData = recurringTransactions.Select(r => new ChartDataDto
        {
            Label = r.Merchant,
            Value = (decimal)r.Amount
        }).ToList();

        var totalMonthly = chartData.Sum(c => c.Value);

        return new ChatResponseDto
        {
            Type = ChatResponseType.Chart,
            Message = $"You have {recurringTransactions.Count} recurring subscriptions totaling ${totalMonthly:N2} per month:",
            ChartData = chartData
        };
    }
}