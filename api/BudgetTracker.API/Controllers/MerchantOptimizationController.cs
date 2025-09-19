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
            var merchantStats = await _context.Merchants
                .GroupBy(m => 1)
                .Select(g => new
                {
                    TotalMerchants = g.Count(),
                    WithEmbeddings = g.Count(m => m.Embedding != null),
                    WithoutEmbeddings = g.Count(m => m.Embedding == null)
                })
                .FirstOrDefaultAsync();

            var embeddingCacheStats = await _context.EmbeddingCache
                .GroupBy(e => 1)
                .Select(g => new
                {
                    TotalCached = g.Count(),
                    TotalUsage = g.Sum(e => e.UsageCount),
                    AvgUsage = g.Average(e => e.UsageCount),
                    OldestEntry = g.Min(e => e.CreatedAt),
                    NewestEntry = g.Max(e => e.CreatedAt)
                })
                .FirstOrDefaultAsync();

            var recentTransactionStats = await _context.Transactions
                .Where(t => t.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .GroupBy(t => 1)
                .Select(g => new
                {
                    TotalTransactions = g.Count(),
                    WithMerchants = g.Count(t => t.MerchantId != null),
                    WithoutMerchants = g.Count(t => t.MerchantId == null),
                    MatchRate = g.Count(t => t.MerchantId != null) / (double)g.Count() * 100
                })
                .FirstOrDefaultAsync();

            var result = new
            {
                merchants = merchantStats ?? new { TotalMerchants = 0, WithEmbeddings = 0, WithoutEmbeddings = 0 },
                embeddingCache = embeddingCacheStats != null ? 
                    new { 
                        TotalCached = embeddingCacheStats.TotalCached,
                        TotalUsage = embeddingCacheStats.TotalUsage,
                        AvgUsage = embeddingCacheStats.AvgUsage,
                        OldestEntry = (DateTime?)embeddingCacheStats.OldestEntry,
                        NewestEntry = (DateTime?)embeddingCacheStats.NewestEntry
                    } : 
                    new { 
                        TotalCached = 0, 
                        TotalUsage = 0, 
                        AvgUsage = 0.0, 
                        OldestEntry = (DateTime?)null, 
                        NewestEntry = (DateTime?)null 
                    },
                recentTransactions = recentTransactionStats ?? new { TotalTransactions = 0, WithMerchants = 0, WithoutMerchants = 0, MatchRate = 0.0 },
                optimization = new
                {
                    estimatedCostSavings = CalculateEstimatedSavings(embeddingCacheStats?.TotalUsage ?? 0),
                    cacheHitRate = embeddingCacheStats?.TotalUsage > 0 ? (embeddingCacheStats.TotalUsage - embeddingCacheStats.TotalCached) / (double)embeddingCacheStats.TotalUsage * 100 : 0,
                    embeddingCoverage = merchantStats?.TotalMerchants > 0 ? merchantStats.WithEmbeddings / (double)merchantStats.TotalMerchants * 100 : 0
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optimization stats");
            return StatusCode(500, new { error = "An error occurred while getting stats" });
        }
    }

    /// <summary>
    /// Test the three-tier matching system performance
    /// </summary>
    [HttpPost("test-performance")]
    public async Task<IActionResult> TestPerformance([FromBody] PerformanceTestRequest request)
    {
        try
        {
            var results = new List<object>();
            
            foreach (var testMerchant in request.TestMerchants)
            {
                var startTime = DateTime.UtcNow;
                var match = await _merchantService.FindBestMatchAsync(testMerchant);
                var endTime = DateTime.UtcNow;
                
                results.Add(new
                {
                    input = testMerchant,
                    match = match != null ? new
                    {
                        merchant = match.Merchant.DisplayName,
                        score = match.SimilarityScore,
                        method = match.MatchMethod
                    } : null,
                    processingTimeMs = (endTime - startTime).TotalMilliseconds,
                    found = match != null
                });
            }

            var avgTime = results.Average(r => (double)r.GetType().GetProperty("processingTimeMs")!.GetValue(r)!);
            var matchRate = results.Count(r => (bool)r.GetType().GetProperty("found")!.GetValue(r)!) / (double)results.Count * 100;

            return Ok(new
            {
                results,
                summary = new
                {
                    totalTests = results.Count,
                    averageTimeMs = avgTime,
                    matchRate = matchRate,
                    estimatedCostPer1000 = EstimateCostPer1000Transactions(results)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing performance");
            return StatusCode(500, new { error = "An error occurred during performance test" });
        }
    }

    /// <summary>
    /// Clean up old, unused embedding cache entries
    /// </summary>
    [HttpPost("cleanup-cache")]
    public async Task<IActionResult> CleanupCache([FromQuery] int maxAgeDays = 30, [FromQuery] int minUsageCount = 2)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-maxAgeDays);
            
            var oldEntries = await _context.EmbeddingCache
                .Where(e => e.LastUsedAt < cutoffDate && e.UsageCount < minUsageCount)
                .ToListAsync();

            _context.EmbeddingCache.RemoveRange(oldEntries);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} old embedding cache entries", oldEntries.Count);

            return Ok(new
            {
                message = $"Cleaned up {oldEntries.Count} old cache entries",
                removedEntries = oldEntries.Count,
                criteria = new
                {
                    maxAgeDays,
                    minUsageCount,
                    cutoffDate
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up cache");
            return StatusCode(500, new { error = "An error occurred during cache cleanup" });
        }
    }

    /// <summary>
    /// Get detailed cache analysis
    /// </summary>
    [HttpGet("cache-analysis")]
    public async Task<IActionResult> GetCacheAnalysis()
    {
        try
        {
            var topUsed = await _context.EmbeddingCache
                .OrderByDescending(e => e.UsageCount)
                .Take(10)
                .Select(e => new
                {
                    e.NormalizedText,
                    e.UsageCount,
                    e.CreatedAt,
                    e.LastUsedAt,
                    daysSinceCreated = (DateTime.UtcNow - e.CreatedAt).TotalDays,
                    daysSinceUsed = (DateTime.UtcNow - e.LastUsedAt).TotalDays
                })
                .ToListAsync();

            var usageDistribution = await _context.EmbeddingCache
                .GroupBy(e => e.UsageCount == 1 ? "1" : 
                            e.UsageCount <= 5 ? "2-5" :
                            e.UsageCount <= 10 ? "6-10" :
                            e.UsageCount <= 50 ? "11-50" : "50+")
                .Select(g => new
                {
                    usageRange = g.Key,
                    count = g.Count(),
                    percentage = g.Count() / (double)_context.EmbeddingCache.Count() * 100
                })
                .ToListAsync();

            return Ok(new
            {
                topUsedCacheEntries = topUsed,
                usageDistribution,
                summary = new
                {
                    totalCacheEntries = await _context.EmbeddingCache.CountAsync(),
                    avgUsageCount = await _context.EmbeddingCache.AverageAsync(e => (double)e.UsageCount),
                    oldestEntry = await _context.EmbeddingCache.MinAsync(e => (DateTime?)e.CreatedAt),
                    newestEntry = await _context.EmbeddingCache.MaxAsync(e => (DateTime?)e.CreatedAt)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache analysis");
            return StatusCode(500, new { error = "An error occurred during cache analysis" });
        }
    }

    private static decimal CalculateEstimatedSavings(int totalUsage)
    {
        // If we didn't have caching, every usage would be a new API call
        var costPerEmbedding = 0.0001m; // ~$0.0001 per embedding
        var cacheBenefit = totalUsage * costPerEmbedding;
        return cacheBenefit;
    }

    private static decimal EstimateCostPer1000Transactions(List<object> results)
    {
        // Count how many would have required new embeddings (Tier 3)
        var newEmbeddingMethods = results.Count(r =>
        {
            var methodProperty = r.GetType().GetProperty("match")?.GetValue(r)?.GetType().GetProperty("method");
            var method = methodProperty?.GetValue(r.GetType().GetProperty("match")!.GetValue(r))?.ToString();
            return method?.Contains("generated") == true;
        });

        var costPer1000 = (decimal)(newEmbeddingMethods / (double)results.Count) * 1000 * 0.0001m;
        return costPer1000;
    }
}

public class PerformanceTestRequest
{
    public string[] TestMerchants { get; set; } = Array.Empty<string>();
}