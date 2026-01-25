using BudgetTracker.API.Services;
using BudgetTracker.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InsightsController : ControllerBase
{
    private readonly IMonthlyInsightsService _monthlyInsightsService;
    private readonly ILogger<InsightsController> _logger;

    public InsightsController(IMonthlyInsightsService monthlyInsightsService, ILogger<InsightsController> logger)
    {
        _monthlyInsightsService = monthlyInsightsService;
        _logger = logger;
    }

    [HttpPost("monthly")]
    public async Task<IActionResult> GetMonthly([FromBody] MonthlyInsightsRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var result = await _monthlyInsightsService.GetMonthlyInsightsAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating monthly insights");
            return StatusCode(500, new { error = "An error occurred while generating monthly insights" });
        }
    }
}

