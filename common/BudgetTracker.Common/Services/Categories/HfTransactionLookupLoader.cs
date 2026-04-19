using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace BudgetTracker.Common.Services.Categories;

/// <summary>
/// Loads transaction-to-category mappings from the Hugging Face transaction-categorization dataset.
/// Data is built by scripts/build-hf-dataset-lookup.py. Used as a fallback when keyword rules don't match.
/// </summary>
public static class HfTransactionLookupLoader
{
    private const int MinPartialMatchScore = 80;
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
        ["Utilities & Services"] = ["Bills & Utilities", "Utilities"],
        ["Financial Services"] = ["Financial Services", "Bank Fees", "Transfer"],
        ["Charity & Donations"] = ["Charity", "Donations"],
        ["Government & Legal"] = ["Government", "Taxes", "Fees"],
        ["Income"] = ["Income", "Salary"],
        ["Travel"] = ["Travel", "Hotel"],
        ["Education"] = ["Education"],
        ["Insurance"] = ["Insurance"],
        ["Personal Care"] = ["Personal Care"],
        ["Transfer"] = ["Transfer"],
        ["Bills & Utilities"] = ["Bills & Utilities", "Utilities"],
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

        var normalizedMerchant = NormalizeText(merchantLower);
        var normalizedDescription = NormalizeText(descriptionLower);
        var normalizedCombined = NormalizeText($"{merchantLower} {descriptionLower}".Trim());

        var cacheKey = $"{normalizedMerchant}|{normalizedDescription}";
        if (_matchCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var lookup = _lookup.Value;
        if (lookup.Count == 0)
        {
            _matchCache.TryAdd(cacheKey, null);
            return null;
        }

        string? hfCategory = null;

        // 1. Try exact matches with both raw and normalized forms.
        var exactCandidates = new[]
        {
            merchantLower,
            descriptionLower,
            $"{merchantLower} {descriptionLower}".Trim(),
            normalizedMerchant,
            normalizedDescription,
            normalizedCombined
        }
        .Where(c => !string.IsNullOrWhiteSpace(c))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in exactCandidates)
        {
            if (lookup.TryGetValue(candidate, out var exactCategory))
            {
                hfCategory = exactCategory;
                break;
            }
        }

        // 2. Score-based partial match for noisy descriptors.
        if (hfCategory == null)
        {
            var searchText = $"{normalizedMerchant} {normalizedDescription}".Trim();
            var searchTokens = GetSignificantTokens(searchText).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var merchantTokens = GetSignificantTokens(normalizedMerchant).ToHashSet(StringComparer.OrdinalIgnoreCase);
            (string Key, string Category, int Score)? best = null;

            foreach (var (key, cat) in lookup)
            {
                if (string.IsNullOrEmpty(key) || key.Length < 4)
                    continue;

                var normalizedKey = NormalizeText(key);
                if (string.IsNullOrEmpty(normalizedKey))
                    continue;

                var score = ScoreMatch(
                    normalizedMerchant,
                    normalizedDescription,
                    searchText,
                    merchantTokens,
                    searchTokens,
                    normalizedKey);

                if (score < MinPartialMatchScore)
                    continue;

                var isBetter = !best.HasValue
                    || score > best.Value.Score
                    || (score == best.Value.Score && normalizedKey.Length < best.Value.Key.Length);

                if (isBetter)
                    best = (normalizedKey, cat, score);
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

        // 1. Try file next to assembly (preferred — file is large, shipped as Content)
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
            // Fall through to embedded resource
        }

        // 2. Try embedded resource (fallback)
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
            // No lookup available
        }

        return result;
    }

    public static void ClearCache() => _matchCache.Clear();

    private static int ScoreMatch(
        string normalizedMerchant,
        string normalizedDescription,
        string searchText,
        HashSet<string> merchantTokens,
        HashSet<string> searchTokens,
        string normalizedKey)
    {
        var score = 0;

        if (!string.IsNullOrEmpty(normalizedMerchant))
        {
            if (normalizedKey == normalizedMerchant)
                return 100;

            if (normalizedKey.StartsWith(normalizedMerchant, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 90);

            // Preserve a guard against short alias false positives.
            if (normalizedMerchant.StartsWith(normalizedKey, StringComparison.OrdinalIgnoreCase)
                && normalizedKey.Length >= 4
                && normalizedKey.Length * 2 >= normalizedMerchant.Length)
            {
                score = Math.Max(score, 82);
            }

            if (normalizedKey.Contains(normalizedMerchant, StringComparison.OrdinalIgnoreCase) && normalizedMerchant.Length >= 4)
                score = Math.Max(score, 78);
        }

        if (!string.IsNullOrEmpty(normalizedDescription))
        {
            if (normalizedKey == normalizedDescription)
                score = Math.Max(score, 88);

            if (normalizedDescription.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase) && normalizedKey.Length >= 8)
                score = Math.Max(score, 70);

            if (normalizedKey.Contains(normalizedDescription, StringComparison.OrdinalIgnoreCase) && normalizedDescription.Length >= 6)
                score = Math.Max(score, 68);
        }

        if (!string.IsNullOrEmpty(searchText))
        {
            if (normalizedKey == searchText)
                score = Math.Max(score, 92);

            if (searchText.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase) && normalizedKey.Length >= 8)
                score = Math.Max(score, 72);
        }

        var keyTokens = GetSignificantTokens(normalizedKey).ToArray();
        if (keyTokens.Length > 0 && searchTokens.Count > 0)
        {
            var overlap = keyTokens.Count(t => searchTokens.Contains(t));
            if (overlap >= 2)
            {
                score = Math.Max(score, 60 + Math.Min(20, overlap * 5));
            }
            else if (overlap == 1)
            {
                var matchedToken = keyTokens.First(t => searchTokens.Contains(t));
                if (matchedToken.Length >= 7 && merchantTokens.Contains(matchedToken))
                    score = Math.Max(score, 58);
            }
        }

        return score;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static IEnumerable<string> GetSignificantTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 4);
    }
}
