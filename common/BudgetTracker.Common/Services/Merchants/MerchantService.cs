using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Services.Embeddings;
using Pgvector;

namespace BudgetTracker.Common.Services.Merchants;

public class MerchantService : IMerchantService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<MerchantService> _logger;

    public MerchantService(
        BudgetTrackerDbContext context,
        IEmbeddingService embeddingService,
        ILogger<MerchantService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<MerchantMatchResult?> FindBestMatchAsync(string rawMerchantName, double similarityThreshold = 0.7)
    {
        if (string.IsNullOrWhiteSpace(rawMerchantName))
            return null;

        var normalizedName = _embeddingService.NormalizeMerchantName(rawMerchantName);
        
        _logger.LogDebug("Finding best match for merchant: '{Raw}' → '{Normalized}'", rawMerchantName, normalizedName);

        // 1. Try exact match first (fastest)
        var exactMatch = await _context.Merchants
            .FirstOrDefaultAsync(m => EF.Functions.ILike(m.DisplayName, normalizedName));
        
        if (exactMatch != null)
        {
            _logger.LogDebug("Found exact match: {Merchant}", exactMatch.DisplayName);
            return new MerchantMatchResult
            {
                Merchant = exactMatch,
                SimilarityScore = 1.0,
                MatchMethod = "exact"
            };
        }

        // 2. Try alias match
        var aliasMatch = await _context.Merchants
            .FirstOrDefaultAsync(m => m.Aliases.Any(alias => EF.Functions.ILike(alias, normalizedName)));
        
        if (aliasMatch != null)
        {
            _logger.LogDebug("Found alias match: {Merchant}", aliasMatch.DisplayName);
            return new MerchantMatchResult
            {
                Merchant = aliasMatch,
                SimilarityScore = 0.95, // High score for alias matches
                MatchMethod = "alias"
            };
        }

        // 3. Use embedding similarity (most sophisticated)
        try
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(normalizedName);
            
            // Find merchants with embeddings and calculate similarity
            var merchantsWithEmbeddings = await _context.Merchants
                .Where(m => m.Embedding != null)
                .ToListAsync();

            if (!merchantsWithEmbeddings.Any())
            {
                _logger.LogWarning("No merchants have embeddings generated yet");
                return null;
            }

            var similarities = merchantsWithEmbeddings
                .Select(m => new MerchantSimilarityResult
                {
                    Merchant = m,
                    SimilarityScore = _embeddingService.CalculateSimilarity(queryEmbedding, m.Embedding!)
                })
                .Where(s => s.SimilarityScore >= similarityThreshold)
                .OrderByDescending(s => s.SimilarityScore)
                .ToList();

            var bestMatch = similarities.FirstOrDefault();
            if (bestMatch != null)
            {
                _logger.LogDebug("Found embedding match: {Merchant} (similarity: {Score:F3})", 
                    bestMatch.Merchant.DisplayName, bestMatch.SimilarityScore);
                
                return new MerchantMatchResult
                {
                    Merchant = bestMatch.Merchant,
                    SimilarityScore = bestMatch.SimilarityScore,
                    MatchMethod = "embedding"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during embedding similarity search for merchant: {Merchant}", normalizedName);
        }

        _logger.LogDebug("No suitable match found for merchant: {Merchant}", normalizedName);
        return null;
    }

    public async Task<Merchant> CreateOrGetMerchantAsync(string merchantName, string? category = null)
    {
        var normalizedName = _embeddingService.NormalizeMerchantName(merchantName);
        
        // Check if merchant already exists
        var existingMerchant = await _context.Merchants
            .FirstOrDefaultAsync(m => EF.Functions.ILike(m.DisplayName, normalizedName));
        
        if (existingMerchant != null)
        {
            return existingMerchant;
        }

        // Create new merchant
        var merchant = new Merchant
        {
            Id = Guid.NewGuid(),
            DisplayName = normalizedName,
            Category = category,
            Aliases = new[] { merchantName.Trim() }, // Store original as alias
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            // Generate embedding for new merchant
            merchant.Embedding = await _embeddingService.GenerateEmbeddingAsync(normalizedName);
            _logger.LogDebug("Generated embedding for new merchant: {Merchant}", normalizedName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for merchant: {Merchant}", normalizedName);
            // Continue without embedding - can be generated later
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
            .Take(50) // Process in batches to avoid rate limits
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