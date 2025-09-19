using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Pgvector;
using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Common.Services.Embeddings;

public class CachedEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _baseService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedEmbeddingService> _logger;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);

    public CachedEmbeddingService(
        IEmbeddingService baseService,
        IMemoryCache cache,
        ILogger<CachedEmbeddingService> logger)
    {
        _baseService = baseService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text)
    {
        var embeddings = await GenerateEmbeddingsAsync(new[] { text });
        return embeddings[0];
    }

    public async Task<Vector[]> GenerateEmbeddingsAsync(string[] texts)
    {
        var results = new Vector[texts.Length];
        var uncachedTexts = new List<(string text, int index)>();

        // Check cache first
        for (int i = 0; i < texts.Length; i++)
        {
            var normalizedText = NormalizeMerchantName(texts[i]);
            var cacheKey = GenerateCacheKey(normalizedText);
            
            if (_cache.TryGetValue(cacheKey, out Vector? cachedEmbedding))
            {
                results[i] = cachedEmbedding!;
                _logger.LogDebug("Cache hit for text: {Text}", normalizedText);
            }
            else
            {
                uncachedTexts.Add((normalizedText, i));
            }
        }

        // Generate embeddings for uncached texts
        if (uncachedTexts.Any())
        {
            _logger.LogDebug("Generating embeddings for {Count} uncached texts", uncachedTexts.Count);
            
            var textsToGenerate = uncachedTexts.Select(x => x.text).ToArray();
            var newEmbeddings = await _baseService.GenerateEmbeddingsAsync(textsToGenerate);

            // Store in cache and results
            for (int i = 0; i < uncachedTexts.Count; i++)
            {
                var (text, originalIndex) = uncachedTexts[i];
                var embedding = newEmbeddings[i];
                var cacheKey = GenerateCacheKey(text);
                
                _cache.Set(cacheKey, embedding, _cacheExpiry);
                results[originalIndex] = embedding;
                
                _logger.LogDebug("Cached embedding for text: {Text}", text);
            }
        }

        return results;
    }

    public double CalculateSimilarity(Vector vector1, Vector vector2)
    {
        return _baseService.CalculateSimilarity(vector1, vector2);
    }

    public string NormalizeMerchantName(string merchantName)
    {
        return _baseService.NormalizeMerchantName(merchantName);
    }

    private static string GenerateCacheKey(string normalizedText)
    {
        // Create deterministic cache key
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedText));
        return $"embedding:{Convert.ToBase64String(bytes)[..16]}"; // Use first 16 chars
    }
}