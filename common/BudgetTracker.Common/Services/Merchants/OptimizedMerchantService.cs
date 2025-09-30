using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.DTOs;
using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Common.Services.Merchants;

public class OptimizedMerchantService : IMerchantService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<OptimizedMerchantService> _logger;

    // Cache settings
    private readonly TimeSpan _memoryCacheExpiry = TimeSpan.FromHours(1);
    private readonly string _merchantCachePrefix = "merchant:";

    // Similarity thresholds
    private const double StringSimilarityThreshold = 0.8;

    public OptimizedMerchantService(
        BudgetTrackerDbContext context,
        IMemoryCache memoryCache,
        ILogger<OptimizedMerchantService> logger)
    {
        _context = context;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<MerchantMatchResult?> FindBestMatchAsync(string rawMerchantName, double similarityThreshold = 0.7)
    {
        if (string.IsNullOrWhiteSpace(rawMerchantName))
            return null;

        var normalizedName = NormalizeMerchantName(rawMerchantName);
        var startTime = DateTime.UtcNow;
        
        _logger.LogDebug("Finding match for: '{Raw}' → '{Normalized}'", rawMerchantName, normalizedName);

        // Use string matching only
        var stringMatch = await TryStringMatchingAsync(normalizedName);
        if (stringMatch != null)
        {
            LogMatchResult("String Match", stringMatch, startTime);
            return stringMatch;
        }

        _logger.LogDebug("No match found for: {Merchant}", normalizedName);
        return null;
    }

    private async Task<MerchantMatchResult?> TryStringMatchingAsync(string normalizedName)
    {
        // Check cache first (using first 15 chars as suggested)
        var keyPrefix = normalizedName.Length > 15 ? normalizedName.Substring(0, 15) : normalizedName;
        var cacheKey = $"{_merchantCachePrefix}{keyPrefix}";
        if (_memoryCache.TryGetValue(cacheKey, out MerchantMatchResult? cachedResult))
        {
            _logger.LogDebug("Cache hit for merchant: {Merchant}", normalizedName);
            return cachedResult;
        }

        // 1. Exact match
        var exactMatch = await _context.Merchants
            .FirstOrDefaultAsync(m => EF.Functions.ILike(m.DisplayName, normalizedName));
        
        if (exactMatch != null)
        {
            var result = new MerchantMatchResult
            {
                Merchant = exactMatch,
                SimilarityScore = 1.0,
                MatchMethod = "exact"
            };
            
            // Cache the result
            _memoryCache.Set(cacheKey, result, _memoryCacheExpiry);
            return result;
        }

        // 2. Common mappings (AMZN → Amazon, etc.)
        var mappedName = StringSimilarityHelper.TryResolveCommonMapping(normalizedName);
        if (mappedName != null)
        {
            var mappingMatch = await _context.Merchants
                .FirstOrDefaultAsync(m => EF.Functions.ILike(m.DisplayName, mappedName));
            
            if (mappingMatch != null)
            {
                var result = new MerchantMatchResult
                {
                    Merchant = mappingMatch,
                    SimilarityScore = 0.95,
                    MatchMethod = "mapping"
                };
                
                // Cache the result
                _memoryCache.Set(cacheKey, result, _memoryCacheExpiry);
                return result;
            }
        }

        // 3. Alias match
        var aliasMatch = await _context.Merchants
            .Where(m => m.Aliases.Any(a => EF.Functions.ILike(a, normalizedName)))
            .FirstOrDefaultAsync();
        
        if (aliasMatch != null)
        {
            var result = new MerchantMatchResult
            {
                Merchant = aliasMatch,
                SimilarityScore = 0.9,
                MatchMethod = "alias"
            };
            
            // Cache the result
            _memoryCache.Set(cacheKey, result, _memoryCacheExpiry);
            return result;
        }

        // 4. Fuzzy string matching (first 15 chars optimization)
        var searchPrefix = normalizedName.Length > 15 ? normalizedName.Substring(0, 15) : normalizedName;
        
        var candidates = await _context.Merchants
            .Where(m => EF.Functions.ILike(m.DisplayName, $"{searchPrefix}%"))
            .ToListAsync();

        foreach (var candidate in candidates)
        {
            var similarity = StringSimilarityHelper.CalculateSimilarity(normalizedName, candidate.DisplayName);
            if (similarity >= StringSimilarityThreshold)
            {
                var result = new MerchantMatchResult
                {
                    Merchant = candidate,
                    SimilarityScore = similarity,
                    MatchMethod = "fuzzy"
                };
                
                // Cache the result
                _memoryCache.Set(cacheKey, result, _memoryCacheExpiry);
                return result;
            }
        }

        return null;
    }

    private static string ComputeTextHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text.ToUpperInvariant()));
        return Convert.ToHexString(bytes);
    }

    private void LogMatchResult(string tier, MerchantMatchResult match, DateTime startTime)
    {
        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogDebug("[{Tier}] Found {Merchant} (score: {Score:F3}, method: {Method}) in {Ms}ms",
            tier, match.Merchant.DisplayName, match.SimilarityScore, match.MatchMethod, elapsed.TotalMilliseconds);
    }

    private static string NormalizeMerchantName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        // Basic normalization - remove common prefixes/suffixes, trim, etc.
        return rawName
            .Replace("*", "")
            .Replace("#", "")
            .Trim()
            .ToUpperInvariant();
    }

    public async Task<Merchant> CreateOrGetMerchantAsync(string merchantName, string? category = null)
    {
        return await CreateMerchantAsync(merchantName, category);
    }

    public async Task<Merchant> CreateMerchantAsync(string normalizedName, string? category = null)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("Merchant name cannot be empty", nameof(normalizedName));

        // Check if merchant already exists
        var existing = await _context.Merchants
            .FirstOrDefaultAsync(m => m.DisplayName == normalizedName);
        
        if (existing != null)
        {
            _logger.LogDebug("Merchant already exists: {Merchant}", normalizedName);
            return existing;
        }

        // Create new merchant
        var merchant = new Merchant
        {
            Id = Guid.NewGuid(),
            DisplayName = normalizedName,
            Category = category ?? "Uncategorized",
            CreatedAt = DateTime.UtcNow,
            Aliases = Array.Empty<string>()
        };

        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new merchant: {Merchant} with category: {Category}", normalizedName, category);
        return merchant;
    }

    public async Task<List<MerchantSimilarityResult>> FindSimilarMerchantsAsync(Guid merchantId, int limit = 10, double minSimilarity = 0.5)
    {
        var sourceMerchant = await _context.Merchants.FindAsync(merchantId);
        if (sourceMerchant == null)
        {
            _logger.LogWarning("Source merchant not found: {MerchantId}", merchantId);
            return new List<MerchantSimilarityResult>();
        }

        // String-based similarity search
        var merchants = await _context.Merchants
            .Where(m => m.Id != merchantId)
            .ToListAsync();

        var results = new List<MerchantSimilarityResult>();

        foreach (var merchant in merchants)
        {
            var similarity = StringSimilarityHelper.CalculateSimilarity(sourceMerchant.DisplayName, merchant.DisplayName);
            
            if (similarity >= minSimilarity)
            {
                results.Add(new MerchantSimilarityResult
                {
                    Merchant = merchant,
                    SimilarityScore = similarity
                });
            }
        }

        return results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(limit)
            .ToList();
    }

    public async Task UpdateMerchantEmbeddingAsync(Guid merchantId)
    {
        // No-op since we removed embedding functionality
        _logger.LogDebug("Embedding update skipped for merchant: {MerchantId}", merchantId);
        await Task.CompletedTask;
    }

    public async Task BatchUpdateEmbeddingsAsync(List<Guid> merchantIds)
    {
        // No-op since we removed embedding functionality
        _logger.LogDebug("Batch embedding update skipped for {Count} merchants", merchantIds.Count);
        await Task.CompletedTask;
    }

    public async Task GenerateMissingEmbeddingsAsync()
    {
        // No-op since we removed embedding functionality
        _logger.LogDebug("Generate missing embeddings skipped - using string-based matching");
        await Task.CompletedTask;
    }

    public async Task<Dictionary<string, object>> GetOptimizationStatsAsync()
    {
        var totalMerchants = await _context.Merchants.CountAsync();
        var merchantsWithAliases = await _context.Merchants.CountAsync(m => m.Aliases.Any());

        return new Dictionary<string, object>
        {
            ["total_merchants"] = totalMerchants,
            ["merchants_with_aliases"] = merchantsWithAliases,
            ["cache_optimization"] = "string-based with 15-char prefix caching",
            ["embedding_status"] = "disabled"
        };
    }
}