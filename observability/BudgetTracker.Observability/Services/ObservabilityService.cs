using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BudgetTracker.Observability.DTOs;
using BudgetTracker.Observability.Interfaces;
using BudgetTracker.Observability.Models;

namespace BudgetTracker.Observability.Services;

public class ObservabilityService : IObservabilityService
{
    private readonly DbContext _context;

    public ObservabilityService(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<LogResponseDto> GetLogsAsync(LogFilterDto filter)
    {
        // Ensure page and pageSize are valid
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize < 1) filter.PageSize = 50;
        
        var query = _context.Set<ApplicationLog>().AsQueryable();

        if (!string.IsNullOrEmpty(filter.Level))
        {
            query = query.Where(l => l.Level == filter.Level);
        }

        if (!string.IsNullOrEmpty(filter.Source))
        {
            // Use case-insensitive comparison and handle null sources
            // PostgreSQL is case-sensitive with quoted identifiers, so we need to use ToLower() for comparison
            // Note: EF Core can translate ToLower() to SQL LOWER() function
            var sourceFilter = filter.Source.Trim().ToLower();
            query = query.Where(l => l.Source != null && l.Source.ToLower() == sourceFilter);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(l => l.Timestamp >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(l => l.Timestamp <= filter.EndDate.Value);
        }

        if (!string.IsNullOrEmpty(filter.SearchText))
        {
            var searchText = filter.SearchText.ToLower();
            query = query.Where(l => 
                l.Message.ToLower().Contains(searchText) ||
                (l.Exception != null && l.Exception.ToLower().Contains(searchText)) ||
                (l.Source != null && l.Source.ToLower().Contains(searchText)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new LogResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<ApplicationLog?> GetLogByIdAsync(Guid id)
    {
        return await _context.Set<ApplicationLog>()
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<List<string>> GetLogLevelsAsync()
    {
        return await _context.Set<ApplicationLog>()
            .Select(l => l.Level)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();
    }

    public async Task<List<string>> GetSourcesAsync()
    {
        return await _context.Set<ApplicationLog>()
            .Where(l => l.Source != null)
            .Select(l => l.Source!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();
    }
}

