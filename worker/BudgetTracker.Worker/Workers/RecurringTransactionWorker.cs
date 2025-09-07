using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;

namespace BudgetTracker.Worker.Workers;

public class RecurringTransactionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurringTransactionWorker> _logger;

    public RecurringTransactionWorker(IServiceProvider serviceProvider, ILogger<RecurringTransactionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectRecurringTransactions(stoppingToken);
                await UpdateRecurringSchedules(stoppingToken);
                
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recurring transactions");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }

    private async Task DetectRecurringTransactions(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();

        var recentTransactions = await context.Transactions
            .Include(t => t.Merchant)
            .Where(t => !t.IsRecurring && t.CreatedAt >= DateTime.UtcNow.AddDays(-90))
            .GroupBy(t => new { t.UserId, NormalizedMerchant = t.NormalizedMerchant ?? t.OriginalMerchant })
            .Where(g => g.Count() >= 3)
            .Select(g => new
            {
                g.Key.UserId,
                Merchant = g.Key.NormalizedMerchant,
                Transactions = g.OrderBy(t => t.TransactionDate).ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var group in recentTransactions)
        {
            if (IsRecurringPattern(group.Transactions, out var pattern))
            {
                var existingSeries = await context.RecurringSeries
                    .FirstOrDefaultAsync(r => r.UserId == group.UserId && 
                                            r.Name == group.Merchant &&
                                            r.IsActive, cancellationToken);

                if (existingSeries == null)
                {
                    var series = new RecurringSeries
                    {
                        Id = Guid.NewGuid(),
                        UserId = group.UserId,
                        Name = group.Merchant,
                        RecurrenceType = pattern.Type,
                        RecurrenceInterval = pattern.Interval,
                        ExpectedAmount = group.Transactions.Average(t => Math.Abs(t.Amount)),
                        AmountTolerance = 0.10m,
                        LastOccurrence = group.Transactions.Max(t => t.TransactionDate),
                        NextExpectedDate = CalculateNextDate(group.Transactions.Max(t => t.TransactionDate), pattern),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.RecurringSeries.Add(series);

                    foreach (var transaction in group.Transactions)
                    {
                        transaction.IsRecurring = true;
                        transaction.RecurringSeriesId = series.Id;
                    }

                    _logger.LogInformation("Detected new recurring series: {SeriesName} for user {UserId}", 
                        series.Name, series.UserId);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateRecurringSchedules(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();

        var activeRecurring = await context.RecurringSeries
            .Where(r => r.IsActive && r.NextExpectedDate < DateTime.UtcNow.AddDays(7))
            .ToListAsync(cancellationToken);

        foreach (var series in activeRecurring)
        {
            var lastTransaction = await context.Transactions
                .Where(t => t.RecurringSeriesId == series.Id)
                .OrderByDescending(t => t.TransactionDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastTransaction != null)
            {
                series.LastOccurrence = lastTransaction.TransactionDate;
                series.NextExpectedDate = CalculateNextDate(
                    lastTransaction.TransactionDate,
                    new RecurrencePattern { Type = series.RecurrenceType, Interval = series.RecurrenceInterval }
                );
                series.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private bool IsRecurringPattern(List<Transaction> transactions, out RecurrencePattern pattern)
    {
        pattern = new RecurrencePattern();
        
        if (transactions.Count < 3) return false;

        var intervals = new List<int>();
        for (int i = 1; i < transactions.Count; i++)
        {
            var daysDiff = (transactions[i].TransactionDate - transactions[i - 1].TransactionDate).Days;
            intervals.Add(daysDiff);
        }

        var avgInterval = intervals.Average();
        var stdDev = Math.Sqrt(intervals.Average(v => Math.Pow(v - avgInterval, 2)));

        if (stdDev > avgInterval * 0.2) return false;

        if (Math.Abs(avgInterval - 30) <= 3)
        {
            pattern.Type = RecurrenceType.Monthly;
            pattern.Interval = 1;
            return true;
        }
        else if (Math.Abs(avgInterval - 7) <= 1)
        {
            pattern.Type = RecurrenceType.Weekly;
            pattern.Interval = 1;
            return true;
        }
        else if (Math.Abs(avgInterval - 14) <= 2)
        {
            pattern.Type = RecurrenceType.BiWeekly;
            pattern.Interval = 1;
            return true;
        }

        return false;
    }

    private DateTime CalculateNextDate(DateTime lastDate, RecurrencePattern pattern)
    {
        return pattern.Type switch
        {
            RecurrenceType.Daily => lastDate.AddDays(pattern.Interval),
            RecurrenceType.Weekly => lastDate.AddDays(7 * pattern.Interval),
            RecurrenceType.BiWeekly => lastDate.AddDays(14 * pattern.Interval),
            RecurrenceType.Monthly => lastDate.AddMonths(pattern.Interval),
            RecurrenceType.Quarterly => lastDate.AddMonths(3 * pattern.Interval),
            RecurrenceType.SemiAnnually => lastDate.AddMonths(6 * pattern.Interval),
            RecurrenceType.Annually => lastDate.AddYears(pattern.Interval),
            _ => lastDate.AddMonths(1)
        };
    }

    private class RecurrencePattern
    {
        public RecurrenceType Type { get; set; }
        public int Interval { get; set; }
    }
}