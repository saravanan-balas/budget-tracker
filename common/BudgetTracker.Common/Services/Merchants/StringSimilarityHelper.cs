using System.Text.RegularExpressions;

namespace BudgetTracker.Common.Services.Merchants;

public static class StringSimilarityHelper
{
    /// <summary>
    /// Calculate Levenshtein distance between two strings
    /// </summary>
    public static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        // Initialize first row and column
        for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    /// <summary>
    /// Calculate similarity score between 0 and 1 (higher = more similar)
    /// </summary>
    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

        int distance = LevenshteinDistance(s1, s2);
        int maxLength = Math.Max(s1.Length, s2.Length);
        return 1.0 - (double)distance / maxLength;
    }

    /// <summary>
    /// Check if two strings are similar enough based on various algorithms
    /// </summary>
    public static bool AreSimilar(string s1, string s2, double threshold = 0.8)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return false;

        // Exact match
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return true;

        // Remove spaces and try again
        var s1Clean = s1.Replace(" ", "").Replace("-", "").Replace("_", "");
        var s2Clean = s2.Replace(" ", "").Replace("-", "").Replace("_", "");
        if (string.Equals(s1Clean, s2Clean, StringComparison.OrdinalIgnoreCase)) return true;

        // Levenshtein similarity
        double similarity = CalculateSimilarity(s1.ToUpper(), s2.ToUpper());
        if (similarity >= threshold) return true;

        // Check if one is contained in the other (for abbreviations)
        if (s1.Length >= 3 && s2.Length >= 3)
        {
            string longer = s1.Length > s2.Length ? s1.ToUpper() : s2.ToUpper();
            string shorter = s1.Length <= s2.Length ? s1.ToUpper() : s2.ToUpper();
            
            if (longer.Contains(shorter) && shorter.Length >= longer.Length * 0.6)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Generate common abbreviations and variations for a merchant name
    /// </summary>
    public static List<string> GenerateVariations(string merchantName)
    {
        var variations = new List<string> { merchantName };
        var upper = merchantName.ToUpper();

        // Remove common words
        var withoutCommon = Regex.Replace(upper, @"\b(THE|AND|CO|CORP|INC|LLC|LTD|COMPANY|STORE|SHOP)\b", "").Trim();
        if (withoutCommon != upper && !string.IsNullOrEmpty(withoutCommon))
        {
            variations.Add(withoutCommon);
        }

        // Generate acronym
        var words = upper.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var acronym = string.Join("", words.Select(w => w[0]));
            if (acronym.Length >= 2)
            {
                variations.Add(acronym);
            }
        }

        // Remove spaces
        var noSpaces = upper.Replace(" ", "").Replace("-", "").Replace("_", "");
        if (noSpaces != upper)
        {
            variations.Add(noSpaces);
        }

        return variations.Distinct().ToList();
    }

    /// <summary>
    /// Common merchant name mappings for known abbreviations
    /// </summary>
    public static readonly Dictionary<string, string> CommonMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Financial
        { "AMZN", "AMAZON" },
        { "PYPL", "PAYPAL" },
        { "SQ", "SQUARE" },
        { "VENMO", "PAYPAL" },
        
        // Food & Dining
        { "MCD", "MCDONALDS" },
        { "SBUX", "STARBUCKS" },
        { "DQ", "DAIRY QUEEN" },
        { "KFC", "KENTUCKY FRIED CHICKEN" },
        { "BK", "BURGER KING" },
        
        // Retail
        { "TGT", "TARGET" },
        { "WMT", "WALMART" },
        { "HD", "HOME DEPOT" },
        { "LOWES", "LOWE'S" },
        
        // Gas Stations
        { "BP", "BRITISH PETROLEUM" },
        { "EXXON", "EXXONMOBIL" },
        { "CHEVRON", "CHEVRON" },
        
        // Technology
        { "GOOG", "GOOGLE" },
        { "MSFT", "MICROSOFT" },
        { "AAPL", "APPLE" },
        { "NFLX", "NETFLIX" },
        
        // Transportation
        { "UBER", "UBER" },
        { "LYFT", "LYFT" },
        
        // Utilities
        { "ATT", "AT&T" },
        { "VZ", "VERIZON" },
        { "CMCSA", "COMCAST" }
    };

    /// <summary>
    /// Try to resolve a merchant name using common mappings
    /// </summary>
    public static string? TryResolveCommonMapping(string merchantName)
    {
        var clean = merchantName.ToUpper().Trim();
        
        // Direct mapping
        if (CommonMappings.TryGetValue(clean, out var directMatch))
        {
            return directMatch;
        }

        // Try to find partial matches
        foreach (var kvp in CommonMappings)
        {
            if (clean.Contains(kvp.Key) && kvp.Key.Length >= 3)
            {
                return kvp.Value;
            }
        }

        return null;
    }
}