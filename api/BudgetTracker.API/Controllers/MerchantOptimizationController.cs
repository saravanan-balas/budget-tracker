using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Services.Merchants;

namespace BudgetTracker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/merchant-optimization")]
public class MerchantOptimizationController : ControllerBase
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IMerchantService _merchantService;
    private readonly ILogger<MerchantOptimizationController> _logger;

    public MerchantOptimizationController(
        BudgetTrackerDbContext context,
        IMerchantService merchantService,
        ILogger<MerchantOptimizationController> logger)
    {
        _context = context;
        _merchantService = merchantService;
        _logger = logger;
    }

    /// <summary>
    /// Get optimization statistics and performance metrics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetOptimizationStats()
    {
        try
        {
            var stats = await _merchantService.GetOptimizationStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optimization stats");
            return StatusCode(500, new { error = "An error occurred while fetching optimization stats" });
        }
    }

    /// <summary>
    /// Get merchant matching performance for a sample of recent transactions
    /// </summary>
    [HttpGet("performance")]
    public async Task<IActionResult> GetPerformanceMetrics([FromQuery] int sampleSize = 100)
    {
        try
        {
            var recentTransactions = await _context.Transactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(sampleSize)
                .Include(t => t.Merchant)
                .ToListAsync();

            var performanceData = new
            {
                sampleSize = recentTransactions.Count,
                transactionsWithMerchants = recentTransactions.Count(t => t.Merchant != null),
                matchRate = recentTransactions.Count > 0 
                    ? (double)recentTransactions.Count(t => t.Merchant != null) / recentTransactions.Count 
                    : 0,
                topMerchants = recentTransactions
                    .Where(t => t.Merchant != null)
                    .GroupBy(t => t.Merchant!.DisplayName)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new { merchant = g.Key, count = g.Count() })
                    .ToList(),
                optimization = "string-based matching with 15-char prefix caching"
            };

            return Ok(performanceData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics");
            return StatusCode(500, new { error = "An error occurred while fetching performance metrics" });
        }
    }

    /// <summary>
    /// Find similar merchants for deduplication opportunities
    /// </summary>
    [HttpGet("merchants/{merchantId}/similar")]
    public async Task<IActionResult> GetSimilarMerchants(Guid merchantId, [FromQuery] int limit = 10, [FromQuery] double minSimilarity = 0.7)
    {
        try
        {
            var similar = await _merchantService.FindSimilarMerchantsAsync(merchantId, limit, minSimilarity);
            return Ok(similar);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding similar merchants for {MerchantId}", merchantId);
            return StatusCode(500, new { error = "An error occurred while finding similar merchants" });
        }
    }

    /// <summary>
    /// Get merchant statistics and usage patterns
    /// </summary>
    [HttpGet("merchant-analysis")]
    public async Task<IActionResult> GetMerchantAnalysis()
    {
        try
        {
            var totalMerchants = await _context.Merchants.CountAsync();
            var merchantsWithAliases = await _context.Merchants.CountAsync(m => m.Aliases.Length > 0);
            var merchantsWithTransactions = await _context.Merchants.CountAsync(m => m.Transactions.Any());
            
            var topMerchantsByTransaction = await _context.Merchants
                .Include(m => m.Transactions)
                .OrderByDescending(m => m.Transactions.Count)
                .Take(10)
                .Select(m => new
                {
                    m.DisplayName,
                    transactionCount = m.Transactions.Count,
                    aliasCount = m.Aliases.Length,
                    m.Category
                })
                .ToListAsync();

            var categoryDistribution = await _context.Merchants
                .GroupBy(m => m.Category ?? "Uncategorized")
                .Select(g => new
                {
                    category = g.Key,
                    count = g.Count()
                })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            return Ok(new
            {
                summary = new
                {
                    totalMerchants,
                    merchantsWithAliases,
                    merchantsWithTransactions,
                    aliasUtilization = totalMerchants > 0 ? (double)merchantsWithAliases / totalMerchants : 0
                },
                topMerchants = topMerchantsByTransaction,
                categoryDistribution,
                optimization = new
                {
                    type = "string-based",
                    caching = "15-char prefix",
                    embeddingStatus = "disabled"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting merchant analysis");
            return StatusCode(500, new { error = "An error occurred during merchant analysis" });
        }
    }

    /// <summary>
    /// Test merchant matching for a given input
    /// </summary>
    [HttpPost("test-match")]
    public async Task<IActionResult> TestMerchantMatch([FromBody] TestMatchRequest request)
    {
        try
        {
            var result = await _merchantService.FindBestMatchAsync(request.MerchantName, request.SimilarityThreshold);
            
            return Ok(new
            {
                input = request.MerchantName,
                threshold = request.SimilarityThreshold,
                match = result != null ? new
                {
                    merchant = result.Merchant.DisplayName,
                    score = result.SimilarityScore,
                    method = result.MatchMethod,
                    category = result.Merchant.Category
                } : null,
                found = result != null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing merchant match for '{MerchantName}'", request.MerchantName);
            return StatusCode(500, new { error = "An error occurred during merchant matching test" });
        }
    }
}

public class TestMatchRequest
{
    public string MerchantName { get; set; } = string.Empty;
    public double SimilarityThreshold { get; set; } = 0.7;
}