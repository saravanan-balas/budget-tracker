using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetTracker.Common.Services.Merchants;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MerchantsController : ControllerBase
{
    private readonly IMerchantService _merchantService;
    private readonly ILogger<MerchantsController> _logger;

    public MerchantsController(IMerchantService merchantService, ILogger<MerchantsController> logger)
    {
        _merchantService = merchantService;
        _logger = logger;
    }

    /// <summary>
    /// Find best matching merchant for a raw merchant name
    /// </summary>
    [HttpPost("match")]
    public async Task<IActionResult> FindBestMatch([FromBody] MerchantMatchRequest request)
    {
        try
        {
            var result = await _merchantService.FindBestMatchAsync(request.MerchantName, request.SimilarityThreshold ?? 0.7);
            
            if (result == null)
            {
                return Ok(new { match = false, message = "No suitable match found" });
            }

            return Ok(new 
            { 
                match = true,
                merchant = new
                {
                    id = result.Merchant.Id,
                    displayName = result.Merchant.DisplayName,
                    category = result.Merchant.Category,
                    aliases = result.Merchant.Aliases
                },
                similarityScore = result.SimilarityScore,
                matchMethod = result.MatchMethod
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding merchant match for: {MerchantName}", request.MerchantName);
            return StatusCode(500, new { error = "An error occurred while finding merchant match" });
        }
    }

    /// <summary>
    /// Generate embeddings for all merchants that don't have them
    /// </summary>
    [HttpPost("generate-embeddings")]
    public async Task<IActionResult> GenerateEmbeddings()
    {
        try
        {
            var count = await _merchantService.GenerateMissingEmbeddingsAsync();
            return Ok(new { message = $"Generated embeddings for {count} merchants" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings");
            return StatusCode(500, new { error = "An error occurred while generating embeddings" });
        }
    }

    /// <summary>
    /// Find similar merchants to a given merchant
    /// </summary>
    [HttpGet("{id}/similar")]
    public async Task<IActionResult> FindSimilar(Guid id, [FromQuery] int limit = 10, [FromQuery] double minSimilarity = 0.5)
    {
        try
        {
            var similarMerchants = await _merchantService.FindSimilarMerchantsAsync(id, limit, minSimilarity);
            
            var result = similarMerchants.Select(s => new
            {
                id = s.Merchant.Id,
                displayName = s.Merchant.DisplayName,
                category = s.Merchant.Category,
                similarityScore = s.SimilarityScore
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding similar merchants for: {MerchantId}", id);
            return StatusCode(500, new { error = "An error occurred while finding similar merchants" });
        }
    }

    /// <summary>
    /// Update embedding for a specific merchant
    /// </summary>
    [HttpPost("{id}/update-embedding")]
    public async Task<IActionResult> UpdateEmbedding(Guid id)
    {
        try
        {
            var merchant = await _merchantService.UpdateMerchantEmbeddingAsync(id);
            return Ok(new { message = $"Updated embedding for merchant: {merchant.DisplayName}" });
        }
        catch (ArgumentException)
        {
            return NotFound(new { error = "Merchant not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating embedding for merchant: {MerchantId}", id);
            return StatusCode(500, new { error = "An error occurred while updating merchant embedding" });
        }
    }

    /// <summary>
    /// Test merchant normalization
    /// </summary>
    [HttpPost("normalize")]
    public async Task<IActionResult> TestNormalization([FromBody] MerchantNormalizationRequest request)
    {
        try
        {
            // This would require access to the embedding service, but demonstrates the concept
            return Ok(new { 
                original = request.MerchantName,
                // normalized = _embeddingService.NormalizeMerchantName(request.MerchantName)
                message = "Normalization endpoint - implementation needed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing normalization for: {MerchantName}", request.MerchantName);
            return StatusCode(500, new { error = "An error occurred during normalization" });
        }
    }
}

public class MerchantMatchRequest
{
    public string MerchantName { get; set; } = string.Empty;
    public double? SimilarityThreshold { get; set; }
}

public class MerchantNormalizationRequest
{
    public string MerchantName { get; set; } = string.Empty;
}