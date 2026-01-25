using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public interface IOpenAiInsightsClient
{
    Task<AiMonthlyInsightsDto?> GenerateMonthlyInsightsAsync(
        MonthlyInsightsResponseDto computed,
        CancellationToken cancellationToken = default);
}

