using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.API.Services;

public class MonthlyInsightsService : IMonthlyInsightsService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IOpenAiInsightsClient _openAiInsightsClient;
    private readonly ILogger<MonthlyInsightsService> _logger;

    public MonthlyInsightsService(
        BudgetTrackerDbContext context,
        IOpenAiInsightsClient openAiInsightsClient,
        ILogger<MonthlyInsightsService> logger)
    {
        _context = context;
        _openAiInsightsClient = openAiInsightsClient;
        _logger = logger;
    }

    public async Task<MonthlyInsightsResponseDto> GetMonthlyInsightsAsync(
        Guid userId,
        MonthlyInsightsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Month < 1 || request.Month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Month), "Month must be between 1 and 12.");
        }

        if (request.Year < 2000 || request.Year > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Year), "Year is out of supported range.");
        }

        var periodStartUtc = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddMonths(1).AddTicks(-1);

        var sampleSize = request.SampleSize.HasValue && request.SampleSize.Value > 0
            ? Math.Min(request.SampleSize.Value, 50)
            : 10;

        _logger.LogInformation(
            "Building monthly insights for user {UserId} {Year}-{Month:00} (accounts: {AccountCount})",
            userId,
            request.Year,
            request.Month,
            request.AccountIds?.Count ?? 0);

        var query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t =>
                t.UserId == userId &&
                t.TransactionDate >= periodStartUtc &&
                t.TransactionDate <= periodEndUtc);

        if (request.AccountIds is { Count: > 0 })
        {
            query = query.Where(t => request.AccountIds.Contains(t.AccountId));
        }

        // Project minimal fields; compute aggregates in-memory to keep logic simple and null-safe.
        var txns = await query
            .Select(t => new
            {
                t.TransactionDate,
                t.Amount,
                t.IsTransfer,
                CategoryName = t.Category != null ? t.Category.Name : null,
                MerchantName = t.NormalizedMerchant ?? t.OriginalMerchant
            })
            .ToListAsync(cancellationToken);

        var nonTransfer = txns.Where(t => !t.IsTransfer).ToList();

        var totalIncome = nonTransfer.Where(t => t.Amount > 0).Sum(t => t.Amount);
        var totalExpenses = nonTransfer.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
        var net = totalIncome - totalExpenses;

        var spendingByCategory = nonTransfer
            .Where(t => t.Amount < 0)
            .GroupBy(t => string.IsNullOrWhiteSpace(t.CategoryName) ? "Uncategorized" : t.CategoryName!)
            .Select(g => new ChartDataDto
            {
                Label = g.Key,
                Value = g.Sum(x => Math.Abs(x.Amount))
            })
            .OrderByDescending(x => x.Value)
            .Take(12)
            .ToList();

        var topMerchants = nonTransfer
            .Where(t => t.Amount < 0)
            .GroupBy(t => string.IsNullOrWhiteSpace(t.MerchantName) ? "Unknown" : t.MerchantName!)
            .Select(g => new MerchantSpendDto
            {
                Merchant = g.Key,
                Amount = g.Sum(x => Math.Abs(x.Amount)),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .Take(10)
            .ToList();

        var sampleTransactions = nonTransfer
            .OrderByDescending(t => Math.Abs(t.Amount))
            .ThenByDescending(t => t.TransactionDate)
            .Take(sampleSize)
            .Select(t => new SampleTransactionDto
            {
                TransactionDateUtc = t.TransactionDate.Kind == DateTimeKind.Utc
                    ? t.TransactionDate
                    : DateTime.SpecifyKind(t.TransactionDate, DateTimeKind.Utc),
                Merchant = t.MerchantName,
                Amount = t.Amount,
                Category = string.IsNullOrWhiteSpace(t.CategoryName) ? "Uncategorized" : t.CategoryName!
            })
            .ToList();

        var topCategory = spendingByCategory.FirstOrDefault();
        var topMerchant = topMerchants.FirstOrDefault();

        var monthLabel = periodStartUtc.ToString("MMMM yyyy");
        var aiFallback = new AiMonthlyInsightsDto
        {
            UsedAi = false,
            Summary =
                $"In {monthLabel}, you had income of ${totalIncome:N2} and expenses of ${totalExpenses:N2} (net {(net >= 0 ? "savings" : "deficit")} of ${Math.Abs(net):N2}).",
            Highlights = new List<string>
            {
                $"Total transactions (excluding transfers): {nonTransfer.Count}.",
                topCategory != null ? $"Top spending category: {topCategory.Label} (${topCategory.Value:N2})." : "No categorized spending found.",
                topMerchant != null ? $"Top merchant: {topMerchant.Merchant} (${topMerchant.Amount:N2} across {topMerchant.Count} transactions)." : "No merchant spending found."
            },
            Suggestions = new List<string>
            {
                "Review your top category and see if any subscriptions or repeat purchases can be reduced.",
                "Check for any unusually large transactions in the sample list and confirm they’re expected.",
                "Set a budget amount for your top categories to track progress next month."
            },
            Watchouts = new List<string>()
        };

        var response = new MonthlyInsightsResponseDto
        {
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            Net = net,
            TransactionCount = nonTransfer.Count,
            SpendingByCategory = spendingByCategory,
            TopMerchants = topMerchants,
            SampleTransactions = sampleTransactions,
            Ai = aiFallback
        };

        // Try to enhance with AI insights (optional). If it fails, keep fallback.
        var ai = await _openAiInsightsClient.GenerateMonthlyInsightsAsync(response, cancellationToken);
        if (ai != null)
        {
            response.Ai = ai;
        }

        return response;
    }
}

