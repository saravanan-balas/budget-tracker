using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BudgetTracker.Common.Services.Categories;

/// <summary>
/// Loads well-known merchant-to-category rules from embedded JSON.
/// Provides high-precision, deterministic matching for common merchants
/// (Walmart, Amazon, Netflix, Uber, etc.) before falling back to HF dataset or AI.
/// </summary>
public static class WellKnownMerchantLoader
{
    private static readonly Lazy<IReadOnlyList<WellKnownMerchantRule>> _rules = new(LoadRules);
    private static readonly ConcurrentDictionary<string, string?> _matchCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tries to match a merchant/description against well-known merchant rules.
    /// Returns the app category name or null if no match.
    /// </summary>
    public static string? TryMatch(string merchant, string? description)
    {
        var merchantLower = merchant?.Trim().ToLowerInvariant() ?? "";
        var descriptionLower = description?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(merchantLower) && string.IsNullOrEmpty(descriptionLower))
            return null;

        var cacheKey = $"{merchantLower}|{descriptionLower}";
        if (_matchCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var searchText = $"{merchantLower} {descriptionLower}".Trim();
        string? result = null;

        foreach (var rule in _rules.Value)
        {
            // Check if any exclude pattern matches first
            if (rule.ExcludePatterns is { Length: > 0 })
            {
                var excluded = false;
                foreach (var exclude in rule.ExcludePatterns)
                {
                    if (searchText.Contains(exclude, StringComparison.OrdinalIgnoreCase))
                    {
                        excluded = true;
                        break;
                    }
                }
                if (excluded) continue;
            }

            // Check if any pattern matches
            foreach (var pattern in rule.Patterns)
            {
                if (searchText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    result = rule.Category;
                    break;
                }
            }

            if (result != null) break;
        }

        _matchCache.TryAdd(cacheKey, result);
        return result;
    }

    private static IReadOnlyList<WellKnownMerchantRule> LoadRules()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "BudgetTracker.Common.Data.WellKnownMerchants.json";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return Array.Empty<WellKnownMerchantRule>();

            var data = JsonSerializer.Deserialize<WellKnownMerchantData>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return data?.Merchants?.ToArray() ?? Array.Empty<WellKnownMerchantRule>();
        }
        catch
        {
            return Array.Empty<WellKnownMerchantRule>();
        }
    }

    public static void ClearCache() => _matchCache.Clear();
}

internal class WellKnownMerchantData
{
    [JsonPropertyName("merchants")]
    public List<WellKnownMerchantRule> Merchants { get; set; } = new();
}

internal class WellKnownMerchantRule
{
    [JsonPropertyName("patterns")]
    public string[] Patterns { get; set; } = Array.Empty<string>();

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("excludePatterns")]
    public string[]? ExcludePatterns { get; set; }
}
