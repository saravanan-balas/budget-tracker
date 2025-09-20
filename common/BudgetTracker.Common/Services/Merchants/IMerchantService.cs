using BudgetTracker.Common.Models;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.Common.Services.Merchants;

public interface IMerchantService
{
    /// <summary>
    /// Find the best matching merchant for a raw merchant name using embedding similarity
    /// </summary>
    /// <param name="rawMerchantName">Raw merchant name from transaction</param>
    /// <param name="similarityThreshold">Minimum similarity score (0.0 to 1.0)</param>
    /// <returns>Best matching merchant or null if no good match found</returns>
    Task<MerchantMatchResult?> FindBestMatchAsync(string rawMerchantName, double similarityThreshold = 0.7);
    
    /// <summary>
    /// Create or get a merchant, generating embeddings if needed
    /// </summary>
    /// <param name="merchantName">Normalized merchant name</param>
    /// <param name="category">Optional category</param>
    /// <returns>Created or existing merchant</returns>
    Task<Merchant> CreateOrGetMerchantAsync(string merchantName, string? category = null);
    
    /// <summary>
    /// Generate and update embedding for a merchant
    /// </summary>
    /// <param name="merchantId">Merchant ID</param>
    /// <returns>Updated merchant</returns>
    Task<Merchant> UpdateMerchantEmbeddingAsync(Guid merchantId);
    
    /// <summary>
    /// Batch process to generate embeddings for all merchants missing them
    /// </summary>
    /// <returns>Number of merchants processed</returns>
    Task<int> GenerateMissingEmbeddingsAsync();
    
    /// <summary>
    /// Find similar merchants to the given merchant
    /// </summary>
    /// <param name="merchantId">Source merchant ID</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="minSimilarity">Minimum similarity threshold</param>
    /// <returns>List of similar merchants with similarity scores</returns>
    Task<List<MerchantSimilarityResult>> FindSimilarMerchantsAsync(Guid merchantId, int limit = 10, double minSimilarity = 0.5);
}