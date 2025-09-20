using Pgvector;

namespace BudgetTracker.Common.Services.Embeddings;

public interface IEmbeddingService
{
    /// <summary>
    /// Generate an embedding vector for the given text
    /// </summary>
    /// <param name="text">Text to generate embedding for</param>
    /// <returns>Vector embedding</returns>
    Task<Vector> GenerateEmbeddingAsync(string text);
    
    /// <summary>
    /// Generate embeddings for multiple texts in batch (more efficient)
    /// </summary>
    /// <param name="texts">List of texts to generate embeddings for</param>
    /// <returns>List of vector embeddings in same order as input</returns>
    Task<Vector[]> GenerateEmbeddingsAsync(string[] texts);
    
    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    /// <param name="vector1">First vector</param>
    /// <param name="vector2">Second vector</param>
    /// <returns>Similarity score between -1 and 1 (higher is more similar)</returns>
    double CalculateSimilarity(Vector vector1, Vector vector2);
    
    /// <summary>
    /// Normalize merchant name for consistent embedding generation
    /// </summary>
    /// <param name="merchantName">Raw merchant name</param>
    /// <returns>Normalized merchant name</returns>
    string NormalizeMerchantName(string merchantName);
}