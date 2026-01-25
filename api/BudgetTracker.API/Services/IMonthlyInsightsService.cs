using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public interface IMonthlyInsightsService
{
    Task<MonthlyInsightsResponseDto> GetMonthlyInsightsAsync(
        Guid userId,
        MonthlyInsightsRequestDto request,
        CancellationToken cancellationToken = default);
}

