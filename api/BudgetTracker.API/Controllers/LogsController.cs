using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BudgetTracker.API.Attributes;
using BudgetTracker.Observability.Interfaces;
using BudgetTracker.Observability.DTOs;
using BudgetTracker.Observability.Models;
using BudgetTracker.Common.Data;

namespace BudgetTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AdminAuthorize]
public class LogsController : ControllerBase
{
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(IObservabilityService observabilityService, ILogger<LogsController> logger)
    {
        _observabilityService = observabilityService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] LogFilterDto filter)
    {
        try
        {
            var result = await _observabilityService.GetLogsAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            return StatusCode(500, new { error = "An error occurred while retrieving logs" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLogById(Guid id)
    {
        try
        {
            var log = await _observabilityService.GetLogByIdAsync(id);
            if (log == null)
            {
                return NotFound();
            }
            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log {LogId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the log" });
        }
    }

    [HttpGet("levels")]
    public async Task<IActionResult> GetLogLevels()
    {
        try
        {
            var levels = await _observabilityService.GetLogLevelsAsync();
            return Ok(levels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log levels");
            return StatusCode(500, new { error = "An error occurred while retrieving log levels" });
        }
    }

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources()
    {
        try
        {
            var sources = await _observabilityService.GetSourcesAsync();
            return Ok(sources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sources");
            return StatusCode(500, new { error = "An error occurred while retrieving sources" });
        }
    }

}

