using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace BudgetTracker.Common.Services.Categories;

/// <summary>
/// Loads transaction-to-category mappings from the Hugging Face transaction-categorization dataset.
/// Data is built by scripts/build-hf-dataset-lookup.py. Used as a fallback when keyword rules don't match.
/// </summary>
public static class HfTransactionLookupLoader
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> _lookup = new(LoadLookup);
    private static readonly ConcurrentDictionary<string, string?> _matchCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// HF dataset categories mapped to app category names (or fallbacks).
    /// </summary>
    private static readonly Dictionary<string, string[]> HfToAppCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Food & Dining"] = ["Food & Dining", "Dining Out", "Groceries", "Restaurants"],
        ["Transportation"] = ["Transportation"],
        ["Shopping & Retail"] = ["Shopping", "Online Shopping"],
        ["Entertainment & Recreation"] = ["Entertainment"],
        ["Healthcare & Medical"] = ["Healthcare"],
        ["Utilities & Services"] = ["Utilities", "Bills & Utilities"],
        ["Financial Services"] = ["Financial Services", "Bank Fees", "Transfer"],
        ["Charity & Donations"] = ["Charity", "Donations"],
        ["Government & Legal"] = ["Government", "Taxes", "Fees"],
        ["Income"] = ["Income", "Salary"],
    };

    /// <summary>
    /// Tries to get a category for the given merchant/description using the HF dataset lookup.
    /// Returns an app category name or null if no match.
    /// </summary>
    public static string? TryGetCategory(string merchant, string? description)
    {
        var merchantLower = merchant?.Trim().ToLowerInvariant() ?? "";
        var descriptionLower = description?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(merchantLower) && string.IsNullOrEmpty(descriptionLower))
            return null;

        var cacheKey = $"{merchantLower}|{descriptionLower}";
        if (_matchCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var lookup = _lookup.Value;
        if (lookup.Count == 0)
        {
            _matchCache.TryAdd(cacheKey, null);
            return null;
        }

        string? hfCategory = null;

        // 1. Try exact matches: merchant, description, or combined
        if (!string.IsNullOrEmpty(merchantLower) && lookup.TryGetValue(merchantLower, out var mCat))
            hfCategory = mCat;
        else if (!string.IsNullOrEmpty(descriptionLower) && lookup.TryGetValue(descriptionLower, out var dCat))
            hfCategory = dCat;
        else if (!string.IsNullOrEmpty(merchantLower) && !string.IsNullOrEmpty(descriptionLower))
        {
            var combined = $"{merchantLower} {descriptionLower}".Trim();
            if (lookup.TryGetValue(combined, out var cCat))
                hfCategory = cCat;
        }

        // 2. Try partial/substring match (HF has full descriptions e.g. "simplisafe home security")
        if (hfCategory == null && (!string.IsNullOrEmpty(merchantLower) || !string.IsNullOrEmpty(descriptionLower)))
        {
            var searchText = $"{merchantLower} {descriptionLower}".Trim();
            (string Key, string Category)? best = null;
            foreach (var (key, cat) in lookup)
            {
                if (string.IsNullOrEmpty(key) || key.Length < 4) continue;
                // Dataset key contains merchant, or merchant/search contains key (min 4 chars to reduce false positives)
                var match = (!string.IsNullOrEmpty(merchantLower) && merchantLower.Length >= 4 && key.Contains(merchantLower, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(merchantLower) && key.Length >= 4 && merchantLower.Contains(key, StringComparison.OrdinalIgnoreCase))
                    || (key.Length >= 4 && searchText.Contains(key, StringComparison.OrdinalIgnoreCase))
                    || (searchText.Length >= 4 && key.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;
                // Prefer when merchant is at start of key, then shorter keys
                var atStart = !string.IsNullOrEmpty(merchantLower) && key.StartsWith(merchantLower, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                var bestAtStart = best.HasValue && !string.IsNullOrEmpty(merchantLower) && best.Value.Key.StartsWith(merchantLower, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                var isBetter = best == null
                    || atStart < bestAtStart
                    || (atStart == bestAtStart && key.Length < best.Value.Key.Length);
                if (isBetter)
                    best = (key, cat);
            }
            if (best.HasValue)
                hfCategory = best.Value.Category;
        }

        var appCategory = hfCategory != null ? MapHfToAppCategory(hfCategory) : null;
        _matchCache.TryAdd(cacheKey, appCategory);
        return appCategory;
    }

    private static string? MapHfToAppCategory(string hfCategory)
    {
        if (HfToAppCategory.TryGetValue(hfCategory, out var fallbacks))
        {
            // Return first (primary) mapping; ResolveCategoryAsync in the service will try fallbacks
            return fallbacks.Length > 0 ? fallbacks[0] : null;
        }
        return hfCategory;
    }

    private static IReadOnlyDictionary<string, string> LoadLookup()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Try embedded resource
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("BudgetTracker.Common.Data.HfTransactionLookup.json");
            if (stream != null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                if (dict != null)
                {
                    foreach (var (k, v) in dict)
                        if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                            result[k] = v;
                    return result;
                }
            }
        }
        catch
        {
            // Fall through to file load
        }

        // 2. Try file next to assembly
        try
        {
            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(asmDir))
            {
                var paths = new[]
                {
                    Path.Combine(asmDir, "Data", "HfTransactionLookup.json"),
                    Path.Combine(asmDir, "HfTransactionLookup.json"),
                };
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (dict != null)
                        {
                            foreach (var (k, v) in dict)
                                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                                    result[k] = v;
                            return result;
                        }
                    }
                }
            }
        }
        catch
        {
            // No lookup available
        }

        return result;
    }

    public static void ClearCache() => _matchCache.Clear();
}
