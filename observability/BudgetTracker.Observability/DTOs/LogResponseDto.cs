using System.Collections.Generic;
using BudgetTracker.Observability.Models;

namespace BudgetTracker.Observability.DTOs;

public class LogResponseDto
{
    public List<ApplicationLog> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

