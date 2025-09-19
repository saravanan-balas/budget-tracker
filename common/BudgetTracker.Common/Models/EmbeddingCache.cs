using Pgvector;

namespace BudgetTracker.Common.Models;

/// <summary>
/// Cache embeddings for raw merchant names to avoid repeated API calls
/// </summary>
public class EmbeddingCache
{
    public Guid Id { get; set; }
    public string NormalizedText { get; set; } = string.Empty;
    public string TextHash { get; set; } = string.Empty; // SHA256 hash for fast lookup
    public Vector Embedding { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public int UsageCount { get; set; }
}