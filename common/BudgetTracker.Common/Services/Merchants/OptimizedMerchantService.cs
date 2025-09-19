using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Services.Embeddings;
using Pgvector;
using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Common.Services.Merchants;

public class OptimizedMerchantService : IMerchantService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<OptimizedMerchantService> _logger;

    // Cache settings
    private readonly TimeSpan _memoryCacheExpiry = TimeSpan.FromHours(1);
    private readonly string _embeddingCachePrefix = "emb:";

    // Similarity thresholds
    private const double StringSimilarityThreshold = 0.8;
    private const double EmbeddingSimilarityThreshold = 0.7;

    public OptimizedMerchantService(
        BudgetTrackerDbContext context,
        IEmbeddingService embeddingService,
        IMemoryCache memoryCache,
        ILogger<OptimizedMerchantService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<MerchantMatchResult?> FindBestMatchAsync(string rawMerchantName, double similarityThreshold = 0.7)
    {
        if (string.IsNullOrWhiteSpace(rawMerchantName))
            return null;

        var normalizedName = _embeddingService.NormalizeMerchantName(rawMerchantName);
        var startTime = DateTime.UtcNow;
        
        _logger.LogDebug("Finding match for: '{Raw}' → '{Normalized}'", rawMerchantName, normalizedName);

        // TIER 1: Fast String Matching (90% of cases, ~5ms, $0)
        var stringMatch = await TryStringMatchingAsync(normalizedName);
        if (stringMatch != null)
        {
            LogMatchResult("Tier 1 (String)", stringMatch, startTime);
            return stringMatch;
        }

        // TIER 2: Cached Embeddings (8% of cases, ~10ms, $0)
        var cachedMatch = await TryCachedEmbeddingMatchAsync(normalizedName, similarityThreshold);
        if (cachedMatch != null)
        {
            LogMatchResult("Tier 2 (Cached)", cachedMatch, startTime);
            return cachedMatch;
        }

        // TIER 3: Generate New Embedding (2% of cases, ~200ms, $0.0001)
        var embeddingMatch = await TryNewEmbeddingMatchAsync(normalizedName, similarityThreshold);
        if (embeddingMatch != null)
        {
            LogMatchResult("Tier 3 (New)", embeddingMatch, startTime);
            return embeddingMatch;
        }

        _logger.LogDebug("No match found for: {Merchant}", normalizedName);
        return null;
    }

    private async Task<MerchantMatchResult?> TryStringMatchingAsync(string normalizedName)
    {
        // 1. Exact match
        var exactMatch = await _context.Merchants
            .FirstOrDefaultAsync(m => EF.Functions.ILike(m.DisplayName, normalizedName));
        
        if (exactMatch != null)
        {
            return new MerchantMatchResult
            {
                Merchant = exactMatch,
                SimilarityScore = 1.0,
                MatchMethod = "exact"
            };
        }

        // 2. Common mappings (AMZN → Amazon, etc.)
        var mappedName = StringSimilarityHelper.TryResolveCommonMapping(normalizedName);
        if (mappedName != null)
        {
            var mappingMatch = await _context.Merchants
                .FirstOrDefaultAsync(m => EF.Functions.ILike(m.DisplayName, mappedName));
            
            if (mappingMatch != null)
            {
                return new MerchantMatchResult
                {
                    Merchant = mappingMatch,
                    SimilarityScore = 0.95,
                    MatchMethod = "mapping"
                };
            }
        }

        // 3. Alias match
        var aliasMatch = await _context.Merchants
            .FirstOrDefaultAsync(m => m.Aliases.Any(alias => EF.Functions.ILike(alias, normalizedName)));
        
        if (aliasMatch != null)
        {
            return new MerchantMatchResult
            {
                Merchant = aliasMatch,
                SimilarityScore = 0.95,
                MatchMethod = "alias"
            };
        }

        // 4. Fuzzy string matching (for typos and variations)
        var merchants = await _context.Merchants
            .Select(m => new { m.Id, m.DisplayName })
            .ToListAsync();

        var fuzzyMatches = merchants
            .Select(m => new { 
                Merchant = m, 
                Similarity = StringSimilarityHelper.CalculateSimilarity(normalizedName, m.DisplayName)
            })
            .Where(x => x.Similarity >= StringSimilarityThreshold)
            .OrderByDescending(x => x.Similarity)
            .FirstOrDefault();

        if (fuzzyMatches != null)
        {
            var fullMerchant = await _context.Merchants.FindAsync(fuzzyMatches.Merchant.Id);
            if (fullMerchant != null)
            {
                return new MerchantMatchResult
                {
                    Merchant = fullMerchant,
                    SimilarityScore = fuzzyMatches.Similarity,
                    MatchMethod = "fuzzy"
                };
            }
        }

        return null;
    }

    private async Task<MerchantMatchResult?> TryCachedEmbeddingMatchAsync(string normalizedName, double threshold)
    {
        var embedding = await GetCachedEmbeddingAsync(normalizedName);
        if (embedding == null) return null;

        return await FindSimilarMerchantByEmbeddingAsync(embedding, threshold, "cached");
    }

    private async Task<MerchantMatchResult?> TryNewEmbeddingMatchAsync(string normalizedName, double threshold)
    {
        try
        {
            _logger.LogInformation("Generating new embedding for: {Merchant} (cost: ~$0.0001)", normalizedName);
            
            var embedding = await _embeddingService.GenerateEmbeddingAsync(normalizedName);
            
            // Cache the new embedding
            await CacheEmbeddingAsync(normalizedName, embedding);
            
            return await FindSimilarMerchantByEmbeddingAsync(embedding, threshold, "generated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for: {Merchant}", normalizedName);
            return null;
        }
    }

    private async Task<Vector?> GetCachedEmbeddingAsync(string normalizedText)
    {
        var textHash = ComputeTextHash(normalizedText);
        var cacheKey = $"{_embeddingCachePrefix}{textHash}";

        // 1. Try memory cache first (fastest - ~0.1ms)
        if (_memoryCache.TryGetValue(cacheKey, out Vector? cachedEmbedding))
        {
            _logger.LogDebug("Memory cache hit for: {Text}", normalizedText);
            return cachedEmbedding;
        }

        // 2. Try database cache (~5ms)
        var dbCached = await _context.EmbeddingCache
            .FirstOrDefaultAsync(e => e.TextHash == textHash);

        if (dbCached != null)
        {
            _logger.LogDebug("Database cache hit for: {Text}", normalizedText);
            
            // Update usage stats
            dbCached.LastUsedAt = DateTime.UtcNow;
            dbCached.UsageCount++;
            
            // Cache in memory for next time
            _memoryCache.Set(cacheKey, dbCached.Embedding, _memoryCacheExpiry);
            
            await _context.SaveChangesAsync();
            return dbCached.Embedding;
        }

        return null;
    }

    private async Task CacheEmbeddingAsync(string normalizedText, Vector embedding)
    {
        var textHash = ComputeTextHash(normalizedText);
        var cacheKey = $"{_embeddingCachePrefix}{textHash}";

        // 1. Save to database
        var cacheEntry = new EmbeddingCache
        {
            Id = Guid.NewGuid(),
            NormalizedText = normalizedText,
            TextHash = textHash,
            Embedding = embedding,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            UsageCount = 1
        };

        _context.EmbeddingCache.Add(cacheEntry);
        await _context.SaveChangesAsync();

        // 2. Cache in memory
        _memoryCache.Set(cacheKey, embedding, _memoryCacheExpiry);
        
        _logger.LogDebug("Cached embedding for: {Text}", normalizedText);
    }

    private async Task<MerchantMatchResult?> FindSimilarMerchantByEmbeddingAsync(Vector queryEmbedding, double threshold, string method)
    {
        var merchantsWithEmbeddings = await _context.Merchants
            .Where(m => m.Embedding != null)
            .ToListAsync();

        if (!merchantsWithEmbeddings.Any())
        {
            _logger.LogWarning("No merchants have embeddings generated yet");
            return null;
        }

        var similarities = merchantsWithEmbeddings
            .Select(m => new 
            {
                Merchant = m,
                Similarity = _embeddingService.CalculateSimilarity(queryEmbedding, m.Embedding!)
            })
            .Where(s => s.Similarity >= threshold)
            .OrderByDescending(s => s.Similarity)
            .FirstOrDefault();

        if (similarities != null)
        {
            return new MerchantMatchResult
            {
                Merchant = similarities.Merchant,
                SimilarityScore = similarities.Similarity,
                MatchMethod = $"embedding-{method}"
            };
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
        _logger.LogDebug("{Tier} match: '{Merchant}' (method: {Method}, score: {Score:F3}, time: {Time}ms)",
            tier, match.Merchant.DisplayName, match.MatchMethod, match.SimilarityScore, elapsed.TotalMilliseconds);
    }

    // Implement remaining interface methods...
    public async Task<Merchant> CreateOrGetMerchantAsync(string merchantName, string? category = null)
    {
        var normalizedName = _embeddingService.NormalizeMerchantName(merchantName);
        
        // Check if merchant already exists using optimized matching
        var existingMatch = await FindBestMatchAsync(merchantName);
        if (existingMatch != null)
        {
            return existingMatch.Merchant;
        }

        // Create new merchant
        var merchant = new Merchant
        {
            Id = Guid.NewGuid(),
            DisplayName = normalizedName,
            Category = category,
            Aliases = new[] { merchantName.Trim() },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            // Generate embedding for new merchant
            merchant.Embedding = await _embeddingService.GenerateEmbeddingAsync(normalizedName);
            
            // Also cache it for future queries
            await CacheEmbeddingAsync(normalizedName, merchant.Embedding);
            
            _logger.LogDebug("Generated embedding for new merchant: {Merchant}", normalizedName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for merchant: {Merchant}", normalizedName);
        }

        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new merchant: {Merchant} with category: {Category}", normalizedName, category);
        return merchant;
    }

    public async Task<Merchant> UpdateMerchantEmbeddingAsync(Guid merchantId)
    {
        var merchant = await _context.Merchants.FindAsync(merchantId);
        if (merchant == null)
        {
            throw new ArgumentException($"Merchant not found: {merchantId}");
        }

        try
        {
            merchant.Embedding = await _embeddingService.GenerateEmbeddingAsync(merchant.DisplayName);
            merchant.UpdatedAt = DateTime.UtcNow;
            
            // Update cache as well
            await CacheEmbeddingAsync(merchant.DisplayName, merchant.Embedding);
            
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Updated embedding for merchant: {Merchant}", merchant.DisplayName);
            return merchant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update embedding for merchant: {Merchant}", merchant.DisplayName);
            throw;
        }
    }

    public async Task<int> GenerateMissingEmbeddingsAsync()
    {
        var merchantsWithoutEmbeddings = await _context.Merchants
            .Where(m => m.Embedding == null)
            .Take(50)
            .ToListAsync();

        if (!merchantsWithoutEmbeddings.Any())
        {
            _logger.LogInformation("All merchants already have embeddings");
            return 0;
        }

        _logger.LogInformation("Generating embeddings for {Count} merchants", merchantsWithoutEmbeddings.Count);

        var merchantNames = merchantsWithoutEmbeddings.Select(m => m.DisplayName).ToArray();
        
        try
        {
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(merchantNames);
            
            for (int i = 0; i < merchantsWithoutEmbeddings.Count; i++)
            {
                merchantsWithoutEmbeddings[i].Embedding = embeddings[i];
                merchantsWithoutEmbeddings[i].UpdatedAt = DateTime.UtcNow;
                
                // Cache the embedding
                await CacheEmbeddingAsync(merchantNames[i], embeddings[i]);
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully generated embeddings for {Count} merchants", merchantsWithoutEmbeddings.Count);
            return merchantsWithoutEmbeddings.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for merchants");
            throw;
        }
    }

    public async Task<List<MerchantSimilarityResult>> FindSimilarMerchantsAsync(Guid merchantId, int limit = 10, double minSimilarity = 0.5)
    {
        var sourceMerchant = await _context.Merchants.FindAsync(merchantId);
        if (sourceMerchant?.Embedding == null)
        {
            _logger.LogWarning("Source merchant not found or has no embedding: {MerchantId}", merchantId);
            return new List<MerchantSimilarityResult>();
        }

        var otherMerchants = await _context.Merchants
            .Where(m => m.Id != merchantId && m.Embedding != null)
            .ToListAsync();

        var similarities = otherMerchants
            .Select(m => new MerchantSimilarityResult
            {
                Merchant = m,
                SimilarityScore = _embeddingService.CalculateSimilarity(sourceMerchant.Embedding, m.Embedding!)
            })
            .Where(s => s.SimilarityScore >= minSimilarity)
            .OrderByDescending(s => s.SimilarityScore)
            .Take(limit)
            .ToList();

        _logger.LogDebug("Found {Count} similar merchants for {Merchant}", similarities.Count, sourceMerchant.DisplayName);
        return similarities;
    }
}