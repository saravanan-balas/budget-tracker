using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace BudgetTracker.Common.Services.Categories;

/// <summary>
/// Loads merchant-to-category keyword mappings from embedded JSON.
/// Data sourced from public MCC (Merchant Category Code) datasets (e.g. greggles/mcc-codes)
/// and can be extended without code changes.
/// </summary>
public static class MerchantCategoryKeywordLoader
{
    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> _keywords = new(LoadKeywords);
    private static readonly ConcurrentDictionary<string, string?> _matchCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to match merchant/description text to a category using keyword rules.
    /// Returns the app category name (e.g. "Groceries", "Dining Out") or null if no match.
    /// </summary>
    public static string? MatchCategory(string merchantLower, string descriptionLower, decimal amount)
    {
        var cacheKey = $"{merchantLower}|{descriptionLower}|{amount}";
        if (_matchCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var searchText = $"{merchantLower} {descriptionLower}";
        var keywords = _keywords.Value;
        string? match = null;

        foreach (var (categoryName, words) in keywords)
        {
            if (words.Any(w => searchText.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                match = categoryName;
                break;
            }
        }

        // Special case: Salary only for positive amounts
        if (match == "Salary" && amount <= 0)
            match = null;

        _matchCache.TryAdd(cacheKey, match);
        return match;
    }

    private static IReadOnlyDictionary<string, string[]> LoadKeywords()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "BudgetTracker.Common.Data.MerchantCategoryKeywords.json";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

            var doc = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(stream);
            if (doc == null || doc.Count == 0)
                return new Dictionary<string, string[]>();

            return doc.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToArray() ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string[]>();
        }
    }

    /// <summary>
    /// Clears the match cache (e.g. after loading updated keyword data).
    /// </summary>
    public static void ClearCache() => _matchCache.Clear();
}
