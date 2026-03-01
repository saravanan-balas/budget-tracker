using BudgetTracker.Common.Models;

namespace BudgetTracker.Common.Services.Categories;

public interface ICategoryAssignmentService
{
    /// <summary>
    /// Assign a category to a transaction based on merchant and description
    /// Uses caching and rule-based assignment before AI fallback
    /// </summary>
    Task<Guid?> AssignCategoryAsync(string merchant, string? description, decimal amount, Guid userId);

    /// <summary>
    /// Batch assign categories to multiple transactions
    /// Optimized for bulk processing during imports
    /// </summary>
    Task<Dictionary<string, Guid?>> BatchAssignCategoriesAsync(
        List<(string merchant, string? description, decimal amount)> transactions, 
        Guid userId);

    /// <summary>
    /// Learn from user category assignments to improve future matching
    /// </summary>
    Task LearnFromAssignmentAsync(string merchant, string? description, decimal amount, Guid categoryId, Guid userId, string source = "AI");

    /// <summary>
    /// Get category assignment statistics and cache performance
    /// </summary>
    Task<Dictionary<string, object>> GetAssignmentStatsAsync();
}