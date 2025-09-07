using System;
using System.Collections.Generic;

namespace BudgetTracker.Common.DTOs;

public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public List<Guid>? AccountIds { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Context { get; set; }
}

public class ChatResponseDto
{
    public string Message { get; set; } = string.Empty;
    public ChatResponseType Type { get; set; }
    public object? Data { get; set; }
    public List<ChartDataDto>? ChartData { get; set; }
    public List<string>? Suggestions { get; set; }
}

public enum ChatResponseType
{
    Text,
    Chart,
    Table,
    Metric,
    Action,
    Error
}

public class ChartDataDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Color { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class MetricDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Unit { get; set; }
    public decimal? Change { get; set; }
    public string? ChangeType { get; set; }
    public string? Period { get; set; }
}