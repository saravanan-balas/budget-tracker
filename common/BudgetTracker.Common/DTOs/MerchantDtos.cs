using BudgetTracker.Common.Models;

namespace BudgetTracker.Common.DTOs;

public class MerchantMatchResult
{
    public Merchant Merchant { get; set; } = null!;
    public double SimilarityScore { get; set; }
    public string MatchMethod { get; set; } = string.Empty; // "embedding", "alias", "exact"
}

public class MerchantSimilarityResult
{
    public Merchant Merchant { get; set; } = null!;
    public double SimilarityScore { get; set; }
}