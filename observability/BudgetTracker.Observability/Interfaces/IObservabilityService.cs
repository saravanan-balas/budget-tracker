using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BudgetTracker.Observability.DTOs;
using BudgetTracker.Observability.Models;

namespace BudgetTracker.Observability.Interfaces;

public interface IObservabilityService
{
    Task<LogResponseDto> GetLogsAsync(LogFilterDto filter);
    Task<ApplicationLog?> GetLogByIdAsync(Guid id);
    Task<List<string>> GetLogLevelsAsync();
    Task<List<string>> GetSourcesAsync();
}

