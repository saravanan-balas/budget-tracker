using System.ComponentModel.DataAnnotations;

namespace BudgetTracker.Common.Models;

/// <summary>
/// Represents a global merchant-to-category mapping shared across all users.
/// This reduces duplicate AI API calls and enables cross-user learning.
/// </summary>
public class GlobalMerchantCategoryMapping
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Normalized merchant name (e.g., "STARBUCKS STORE", "WHOLE FOODS")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string MerchantName { get; set; } = string.Empty;

    /// <summary>
    /// Category name (not ID, since categories are user-specific).
    /// Resolved to user's CategoryId at runtime via ResolveCategoryAsync.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Number of users who confirmed this mapping.
    /// Increments when multiple users get the same category for this merchant.
    /// </summary>
    public int ConfirmationCount { get; set; } = 1;

    /// <summary>
    /// Confidence score - starts at 1.0, increases by 0.1 per confirmation.
    /// Used to prioritize high-confidence mappings.
    /// </summary>
    public decimal ConfidenceScore { get; set; } = 1.0m;

    /// <summary>
    /// Source of this mapping: WellKnown, HF, MCC, AI.
    /// Used for debugging, analytics, and conflict resolution.
    /// </summary>
    [MaxLength(50)]
    public string Source { get; set; } = "AI";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
