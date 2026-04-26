using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;

namespace BudgetTracker.Common.Services.Categories;

public class OptimizedCategoryAssignmentService : ICategoryAssignmentService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<OptimizedCategoryAssignmentService> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Cache settings
    private readonly TimeSpan _memoryCacheExpiry = TimeSpan.FromHours(2);
    private readonly string _categoryMappingCachePrefix = "cat_mapping:";
    private readonly string _merchantCategoryCachePrefix = "merchant_cat:";
    private readonly string _ruleCachePrefix = "cat_rule:";

    /// <summary>When true, skip UserMerchantCategoryMappings (for testing MCC/HF/ML). Set via Categorization__SkipMerchantMappings or SKIP_MERCHANT_MAPPINGS.</summary>
    private static bool SkipMerchantMappings =>
        string.Equals(Environment.GetEnvironmentVariable("SKIP_MERCHANT_MAPPINGS"), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("Categorization__SkipMerchantMappings"), "true", StringComparison.OrdinalIgnoreCase);

    // Performance counters
    private long _cacheHits = 0;
    private long _wellKnownMappings = 0;
    private long _hfLookupMappings = 0;
    private long _ruleMappings = 0;
    private long _globalMappings = 0;
    private long _merchantMappings = 0;
    private long _aiFallbacks = 0;
    private long _parsedFallbackMappings = 0;

    public OptimizedCategoryAssignmentService(
        BudgetTrackerDbContext context,
        IMemoryCache memoryCache,
        ILogger<OptimizedCategoryAssignmentService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _memoryCache = memoryCache;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<Guid?> AssignCategoryAsync(string merchant, string? description, decimal amount, Guid userId)
    {
        var cacheKey = GenerateCacheKey(merchant, description, amount, userId);

        // 1. Check cache first
        if (_memoryCache.TryGetValue(cacheKey, out Guid? cachedCategoryId))
        {
            Interlocked.Increment(ref _cacheHits);
            return cachedCategoryId;
        }

        // 2. Try learned merchant mappings first (user-specific overrides should win).
        if (!SkipMerchantMappings)
        {
            var merchantCategory = await TryMerchantBasedAssignment(merchant, userId);
            if (merchantCategory.HasValue)
            {
                Interlocked.Increment(ref _merchantMappings);
                _memoryCache.Set(cacheKey, merchantCategory.Value, _memoryCacheExpiry);
                return merchantCategory.Value;
            }
        }

        // 3. Try global merchant mappings (shared across all users).
        var globalCategory = await TryGlobalMerchantAssignment(merchant, userId);
        if (globalCategory.HasValue)
        {
            Interlocked.Increment(ref _globalMappings);
            _memoryCache.Set(cacheKey, globalCategory.Value, _memoryCacheExpiry);
            return globalCategory.Value;
        }

        // 4. Try deterministic rules.
        var wellKnownCategory = await TryWellKnownMerchantAssignment(merchant, description, userId);
        if (wellKnownCategory.HasValue)
        {
            Interlocked.Increment(ref _wellKnownMappings);
            _memoryCache.Set(cacheKey, wellKnownCategory.Value, _memoryCacheExpiry);
            return wellKnownCategory.Value;
        }

        var hfCategory = await TryHfDatasetAssignment(merchant, description, userId);
        if (hfCategory.HasValue)
        {
            Interlocked.Increment(ref _hfLookupMappings);
            _memoryCache.Set(cacheKey, hfCategory.Value, _memoryCacheExpiry);
            return hfCategory.Value;
        }

        var ruleCategory = await TryRuleBasedAssignment(merchant, description, amount, userId);
        if (ruleCategory.HasValue)
        {
            Interlocked.Increment(ref _ruleMappings);
            _memoryCache.Set(cacheKey, ruleCategory.Value, _memoryCacheExpiry);
            return ruleCategory.Value;
        }

        // 5. AI fallback (most expensive - result is persisted to avoid repeat calls).
        var aiCategory = await TryAiAssignment(merchant, description, amount, userId);
        if (aiCategory.HasValue)
        {
            Interlocked.Increment(ref _aiFallbacks);
            _memoryCache.Set(cacheKey, aiCategory.Value, _memoryCacheExpiry);
            return aiCategory;
        }

        var fallbackCategory = await GetFallbackUncategorizedAsync(merchant, userId);
        if (fallbackCategory.HasValue)
        {
            _memoryCache.Set(cacheKey, fallbackCategory.Value, _memoryCacheExpiry);
        }

        return fallbackCategory;
    }

    public async Task<Dictionary<string, Guid?>> BatchAssignCategoriesAsync(
        List<(string merchant, string? description, decimal amount, string? parsedCategoryHint)> transactions,
        Guid userId)
    {
        var results = new Dictionary<string, Guid?>();
        var uncachedTransactions = new List<(string key, string merchant, string? description, decimal amount, string? parsedCategoryHint)>();

        // 1. Check cache for all transactions first
        foreach (var (merchant, description, amount, parsedCategoryHint) in transactions)
        {
            var cacheKey = GenerateCacheKey(merchant, description, amount, userId, parsedCategoryHint);
            var lookupKey = BuildLookupKey(merchant, description, amount, parsedCategoryHint);

            if (_memoryCache.TryGetValue(cacheKey, out Guid? cachedCategoryId))
            {
                results[lookupKey] = cachedCategoryId;
                Interlocked.Increment(ref _cacheHits);
            }
            else
            {
                uncachedTransactions.Add((lookupKey, merchant, description, amount, parsedCategoryHint));
            }
        }

        if (uncachedTransactions.Count == 0)
        {
            return results;
        }

        // 2. Batch load merchant categories for uncached transactions (skipped when SkipMerchantMappings).
        var merchants = uncachedTransactions.Select(t => t.merchant).Distinct().ToList();
        var merchantCategories = SkipMerchantMappings
            ? new Dictionary<string, Guid?>()
            : await GetMerchantCategoriesAsync(merchants, userId);

        // 3. Process uncached transactions (pipeline: user overrides -> global -> well-known -> HF -> MCC -> AI)
        var batchWellKnown = 0;
        var batchHf = 0;
        var batchMcc = 0;
        var batchGlobal = 0;
        var batchMerchant = 0;
        var batchAi = 0;
        var batchParsedFallback = 0;
        foreach (var (lookupKey, merchant, description, amount, parsedCategoryHint) in uncachedTransactions)
        {
            Guid? categoryId = null;
            _logger.LogDebug("Processing uncached transaction: {LookupKey} (merchant: {Merchant})", lookupKey, merchant);

            // User-specific override first.
            if (merchantCategories.TryGetValue(merchant, out var merchantCat) && merchantCat.HasValue)
            {
                categoryId = merchantCat;
                Interlocked.Increment(ref _merchantMappings);
                batchMerchant++;
                _logger.LogDebug("Merchant-based (user override) assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                await LearnFromAssignmentAsync(merchant, description, amount, categoryId.Value, userId, "User");
            }
            // Then global learned mapping.
            else if ((categoryId = await TryGlobalMerchantAssignment(merchant, userId)).HasValue)
            {
                Interlocked.Increment(ref _globalMappings);
                batchGlobal++;
                _logger.LogDebug("Global merchant assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
            }
            // Deterministic rule layers.
            else if ((categoryId = await TryWellKnownMerchantAssignment(merchant, description, userId)).HasValue)
            {
                Interlocked.Increment(ref _wellKnownMappings);
                batchWellKnown++;
                _logger.LogDebug("Well-known merchant assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
            }
            else if ((categoryId = await TryHfDatasetAssignment(merchant, description, userId)).HasValue)
            {
                Interlocked.Increment(ref _hfLookupMappings);
                batchHf++;
                _logger.LogDebug("Hugging Face dataset assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
            }
            else if ((categoryId = await TryRuleBasedAssignment(merchant, description, amount, userId)).HasValue)
            {
                Interlocked.Increment(ref _ruleMappings);
                batchMcc++;
                _logger.LogDebug("Rule-based (MCC) assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
            }
            else
            {
                // AI fallback first.
                categoryId = await TryAiAssignment(merchant, description, amount, userId);
                if (categoryId.HasValue)
                {
                    Interlocked.Increment(ref _aiFallbacks);
                    batchAi++;
                    _logger.LogDebug("AI fallback assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                }
                else
                {
                    categoryId = await TryParsedCategoryAssignment(parsedCategoryHint, userId);
                    if (categoryId.HasValue)
                    {
                        Interlocked.Increment(ref _parsedFallbackMappings);
                        batchParsedFallback++;
                        _logger.LogDebug(
                            "Parsed-category fallback assignment successful for {Merchant}: {CategoryId} (hint: {ParsedCategoryHint})",
                            merchant, categoryId, parsedCategoryHint);
                    }
                    else
                    {
                        categoryId = await GetFallbackUncategorizedAsync(merchant, userId);
                        _logger.LogWarning("All categorization methods failed for {Merchant}; using Uncategorized fallback", merchant);
                    }
                }
            }

            results[lookupKey] = categoryId;
            _logger.LogDebug("Storing result for key '{LookupKey}': {CategoryId}", lookupKey, categoryId?.ToString() ?? "null");

            // Cache the result
            var cacheKey = GenerateCacheKey(merchant, description, amount, userId, parsedCategoryHint);
            if (categoryId.HasValue)
            {
                _memoryCache.Set(cacheKey, categoryId.Value, _memoryCacheExpiry);
            }
        }

        _logger.LogInformation(
            "Categorization batch complete: {MerchantCount} from user mappings, {GlobalCount} from global mappings, {WellKnownCount} from well-known merchants, {HfCount} from Hugging Face dataset, {MccCount} from MCC keywords, {AiCount} from AI fallback, {ParsedFallbackCount} from parsed-category fallback (processed {Total} uncached)",
            batchMerchant, batchGlobal, batchWellKnown, batchHf, batchMcc, batchAi, batchParsedFallback, uncachedTransactions.Count);

        return results;
    }

    public async Task LearnFromAssignmentAsync(string merchant, string? description, decimal amount, Guid categoryId, Guid userId, string source = "AI")
    {
        try
        {
            // For automatic sources (AI/HF/MCC/WellKnown), only store in global table
            // User-level mappings are only created for "User" source (manual overrides)
            if (source == "User")
            {
                // User explicitly changed/set this mapping - store in user table as an override
                var existingMapping = await _context.UserMerchantCategoryMappings
                    .FirstOrDefaultAsync(m => m.UserId == userId && m.MerchantName == merchant);

                if (existingMapping == null)
                {
                    // Create the user override mapping
                    var newMapping = new UserMerchantCategoryMapping
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        MerchantName = merchant,
                        CategoryId = categoryId,
                        ConfidenceScore = 1.0m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserMerchantCategoryMappings.Add(newMapping);
                    _logger.LogDebug("Created user override mapping: {Merchant} → {CategoryId} for user {UserId}",
                        merchant, categoryId, userId);
                }
                else
                {
                    // Update existing user override
                    existingMapping.CategoryId = categoryId;
                    existingMapping.ConfidenceScore += 0.1m;
                    existingMapping.UpdatedAt = DateTime.UtcNow;
                    _logger.LogDebug("Updated user override mapping: {Merchant} → {CategoryId} for user {UserId}",
                        merchant, categoryId, userId);
                }
            }
            else
            {
                // For AI/HF/MCC/WellKnown sources, only update global mapping (no user-level duplication)
                await UpdateGlobalMappingAsync(merchant, categoryId, source);
            }

            await _context.SaveChangesAsync();

            // Invalidate cache for this merchant
            var cacheKey = GenerateCacheKey(merchant, description, amount, userId);
            _memoryCache.Remove(cacheKey);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error learning from category assignment for merchant: {Merchant}", merchant);
        }
    }

    private async Task<Guid?> TryWellKnownMerchantAssignment(string merchant, string? description, Guid userId)
    {
        var matchedCategory = WellKnownMerchantLoader.TryMatch(merchant, description);
        if (matchedCategory == null)
            return null;

        var categoryId = await ResolveCategoryAsync(matchedCategory, userId);
        if (categoryId.HasValue)
        {
            _logger.LogInformation("Assigned {Merchant} to {Category} (well-known merchant)", merchant, matchedCategory);
            return categoryId.Value;
        }
        return null;
    }

    private async Task<Guid?> TryRuleBasedAssignment(string merchant, string? description, decimal amount, Guid userId)
    {
        var ruleKey = $"{_ruleCachePrefix}{merchant.ToLowerInvariant()}";

        if (_memoryCache.TryGetValue(ruleKey, out Guid? cachedRule))
        {
            _logger.LogDebug("Rule cache hit for merchant: {Merchant}", merchant);
            return cachedRule;
        }

        var merchantLower = merchant.ToLowerInvariant();
        var descriptionLower = description?.ToLowerInvariant() ?? "";

        var matchedCategory = MerchantCategoryKeywordLoader.MatchCategory(merchantLower, descriptionLower, amount);
        if (matchedCategory != null)
        {
            var category = await ResolveCategoryAsync(matchedCategory, userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                _logger.LogInformation("Assigned {Merchant} to {Category} (MCC keywords)", merchant, matchedCategory);
                return category.Value;
            }
        }

        return null;
    }

    private async Task<Guid?> TryHfDatasetAssignment(string merchant, string? description, Guid userId)
    {
        var matchedCategory = HfTransactionLookupLoader.TryGetCategory(merchant, description);
        if (matchedCategory == null)
            return null;

        var categoryId = await ResolveCategoryAsync(matchedCategory, userId);
        if (categoryId.HasValue)
        {
            _logger.LogInformation("Assigned {Merchant} to {Category} (Hugging Face dataset)", merchant, matchedCategory);
            return categoryId.Value;
        }
        return null;
    }

    /// <summary>
    /// Resolves a matched category name to a Guid, trying fallback names when the primary doesn't exist.
    /// </summary>
    private async Task<Guid?> ResolveCategoryAsync(string categoryName, Guid userId)
    {
        var fallbacks = CategoryFallbacks.TryGetValue(categoryName, out var fb) ? fb : new[] { categoryName };
        foreach (var name in fallbacks)
        {
            var id = await GetCategoryByNameAsync(name, userId);
            if (id.HasValue) return id;
        }
        return null;
    }

    private static readonly Dictionary<string, string[]> CategoryFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dining Out"] = new[] { "Dining Out", "Restaurants", "Food & Dining" },
        ["Food & Dining"] = new[] { "Food & Dining", "Dining Out", "Groceries", "Restaurants" },
        ["Hotel"] = new[] { "Hotel", "Travel" },
        ["Travel"] = new[] { "Travel", "Hotel" },
        ["Pet Care"] = new[] { "Pet Care", "Pets", "Personal Care" },
        ["Shopping"] = new[] { "Shopping", "Online Shopping" },
        ["Subscriptions"] = new[] { "Subscriptions", "Software", "Technology" },
        ["Alcohol"] = new[] { "Alcohol", "Beverages", "Food & Dining" },
        ["Transportation"] = new[] { "Transportation" },
        ["Groceries"] = new[] { "Groceries" },
        ["Healthcare"] = new[] { "Healthcare" },
        ["Utilities"] = new[] { "Utilities", "Bills & Utilities" },
        ["Bills & Utilities"] = new[] { "Bills & Utilities", "Utilities" },
        ["Entertainment"] = new[] { "Entertainment" },
        ["Charity"] = new[] { "Charity", "Donations" },
        ["Financial Services"] = new[] { "Financial Services", "Bank Fees", "Transfer" },
        ["Government"] = new[] { "Government", "Taxes", "Fees" },
        ["Income"] = new[] { "Income", "Salary" },
        ["Transfer"] = new[] { "Transfer" },
        ["Education"] = new[] { "Education" },
        ["Insurance"] = new[] { "Insurance" },
        ["Personal Care"] = new[] { "Personal Care" },
        ["Child Care"] = new[] { "Child Care" },
        ["Legal Services"] = new[] { "Legal Services" },
    };

    private async Task<Guid?> TryGlobalMerchantAssignment(string merchant, Guid userId)
    {
        var normalizedMerchant = NormalizeMerchantForMapping(merchant);

        var globalMapping = await _context.GlobalMerchantCategoryMappings
            .Where(m => m.MerchantName == normalizedMerchant)
            .OrderByDescending(m => m.ConfidenceScore)
            .FirstOrDefaultAsync();

        if (globalMapping == null)
            return null;

        var categoryId = await ResolveCategoryAsync(globalMapping.CategoryName, userId);
        if (categoryId.HasValue)
        {
            _logger.LogInformation(
                "Assigned {OriginalMerchant} (normalized: {NormalizedMerchant}) to {Category} (global mapping, confidence: {Score})",
                merchant, normalizedMerchant, globalMapping.CategoryName, globalMapping.ConfidenceScore);
            return categoryId.Value;
        }
        return null;
    }

    private async Task UpdateGlobalMappingAsync(string merchant, Guid categoryId, string source)
    {
        var category = await _context.Categories.FindAsync(categoryId);
        if (category == null) return;

        var normalizedMerchant = NormalizeMerchantForMapping(merchant);

        var existing = await _context.GlobalMerchantCategoryMappings
            .FirstOrDefaultAsync(m => m.MerchantName == normalizedMerchant);

        if (existing == null)
        {
            // Create new global mapping
            _context.GlobalMerchantCategoryMappings.Add(new GlobalMerchantCategoryMapping
            {
                Id = Guid.NewGuid(),
                MerchantName = normalizedMerchant,
                CategoryName = category.Name,
                ConfirmationCount = 1,
                ConfidenceScore = 1.0m,
                Source = source,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            _logger.LogDebug("Created global mapping: {OriginalMerchant} → {NormalizedMerchant} → {Category} ({Source})",
                merchant, normalizedMerchant, category.Name, source);
        }
        else
        {
            // Update existing - increase confidence if same category
            if (existing.CategoryName == category.Name)
            {
                existing.ConfirmationCount++;
                existing.ConfidenceScore += 0.1m;
                existing.UpdatedAt = DateTime.UtcNow;
                _logger.LogDebug("Increased confidence for global mapping: {NormalizedMerchant} → {Category} (count: {Count}, score: {Score})",
                    normalizedMerchant, category.Name, existing.ConfirmationCount, existing.ConfidenceScore);
            }
            else
            {
                // Different category - only update if new source has higher priority
                if (GetSourcePriority(source) > GetSourcePriority(existing.Source))
                {
                    _logger.LogWarning(
                        "Global mapping conflict for {NormalizedMerchant}: {OldCat} ({OldSrc}) → {NewCat} ({NewSrc})",
                        normalizedMerchant, existing.CategoryName, existing.Source, category.Name, source);
                    existing.CategoryName = category.Name;
                    existing.ConfirmationCount = 1;
                    existing.ConfidenceScore = 1.0m;
                    existing.Source = source;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }

    private static int GetSourcePriority(string source) => source switch
    {
        "WellKnown" => 4,
        "HF" => 3,
        "MCC" => 2,
        "AI" => 1,
        _ => 0
    };

    /// <summary>
    /// Normalizes merchant names for global mapping storage by removing variable parts
    /// (account numbers, confirmation numbers, personal names, etc.) to enable cross-user matching.
    /// </summary>
    /// <param name="merchant">Raw merchant/transaction description</param>
    /// <returns>Normalized merchant name suitable for global mappings</returns>
    private static string NormalizeMerchantForMapping(string merchant)
    {
        if (string.IsNullOrWhiteSpace(merchant))
            return merchant;

        var normalized = merchant.ToUpperInvariant().Trim();

        // Remove confirmation numbers first (various patterns)
        // Examples: "Confirmation# XXXXX86417", "Conf# 99c1pylzx", "CONF #123456"
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\bCONF(?:IRMATION)?\s*#?\s*[A-Z0-9]+",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove semicolons early (often used as separators before conf numbers)
        normalized = normalized.Replace(";", " ");

        // For Zelle/Venmo/payment patterns, remove everything after "from/to" (includes personal names and purposes)
        // Examples: "Zelle payment from JOHN DOE for rent" → "ZELLE PAYMENT"
        // Examples: "Venmo to JANE SMITH for dinner" → "VENMO"
        if (normalized.Contains("ZELLE") || normalized.Contains("VENMO") || normalized.Contains("PAYMENT"))
        {
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"\b(?:FROM|TO)\s+.+$",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Remove account type + number patterns
        // Examples: "BRK 2826", "CHK 7794", "SAV 6119", "ACCT 1234"
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\b(?:BRK|CHK|SAV|ACCT|ACCOUNT)\s*\d+",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove standalone "TO" or "FROM" words (often left over after account removal)
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\b(?:TO|FROM)\b",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove details after "for" (transaction-specific purposes)
        // Examples: "for burlington", "for rent payment"
        var forIndex = normalized.IndexOf(" FOR ", StringComparison.OrdinalIgnoreCase);
        if (forIndex > 0)
        {
            normalized = normalized.Substring(0, forIndex);
        }

        // Remove extra whitespace
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        // Remove common business suffixes at the end
        // Must be at word boundaries to avoid removing "INCORPORATED" from "INCORPORATED BANK"
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\s+(?:INC\.?|LLC|LTD\.?|CORP\.?|CORPORATION|CO\.?|COMPANY)\s*$",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return normalized.Trim();
    }

    private async Task<Guid?> TryMerchantBasedAssignment(string merchant, Guid userId)
    {
        var normalizedMerchant = NormalizeMerchantForMapping(merchant);
        var merchantCacheKey = $"{_merchantCategoryCachePrefix}{userId}:{normalizedMerchant}";

        if (_memoryCache.TryGetValue(merchantCacheKey, out Guid? cachedMerchantCategory))
        {
            return cachedMerchantCategory;
        }

        // Look up learned merchant-category mappings using LINQ
        var mappingEntity = await _context.UserMerchantCategoryMappings
            .Where(m => m.UserId == userId && (m.MerchantName == merchant || m.MerchantName == normalizedMerchant))
            .OrderByDescending(m => m.ConfidenceScore)
            .ThenByDescending(m => m.UpdatedAt)
            .FirstOrDefaultAsync();

        var mapping = mappingEntity?.CategoryId;

        if (mapping.HasValue)
        {
            _memoryCache.Set(merchantCacheKey, mapping.Value, _memoryCacheExpiry);
        }

        return mapping;
    }

    private async Task<Guid?> TryAiAssignment(string merchant, string? description, decimal amount, Guid userId)
    {
        _logger.LogDebug("Attempting AI fallback assignment for {Merchant}", merchant);

        if (!IsAiFallbackEnabled())
        {
            _logger.LogInformation("AI fallback disabled or misconfigured for {Merchant}", merchant);
            return null;
        }

        try
        {
            // Try AI categorization as fallback
            var aiAnalyzer = _serviceProvider.GetService(typeof(AI.IAIBankAnalyzer)) as AI.IAIBankAnalyzer;
            if (aiAnalyzer != null)
            {
                var prompt = $@"Categorize this financial transaction into one of these categories:
Food & Dining, Groceries, Transportation, Shopping, Entertainment, Bills & Utilities, Healthcare, Travel, Education,
Personal Care, Home & Garden, Business, Investments, Income, Transfer, Charity, Insurance.

Transaction: {merchant} - {description} - Amount: ${amount:F2}

Return only the category name:";

                var aiCategory = await aiAnalyzer.CategorizeTransactionAsync(prompt);
                if (!string.IsNullOrEmpty(aiCategory) &&
                    !aiCategory.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase) &&
                    !aiCategory.Equals("Miscellaneous", StringComparison.OrdinalIgnoreCase))
                {
                    var category = await ResolveCategoryAsync(aiCategory, userId);
                    if (category.HasValue)
                    {
                        _logger.LogDebug("AI assignment successful for {Merchant}: {Category}", merchant, aiCategory);

                        // Persist AI result so we don't hit OpenAI again for this merchant
                        await LearnFromAssignmentAsync(merchant, description, amount, category.Value, userId);
                        return category.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI fallback failed for {Merchant}", merchant);
        }

        return null;
    }

    private async Task<Guid?> TryParsedCategoryAssignment(string? parsedCategoryHint, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(parsedCategoryHint))
            return null;

        var normalizedHint = parsedCategoryHint.Trim();
        if (ParsedCategoryFallbacks.TryGetValue(normalizedHint, out var mappedCategory))
        {
            return await ResolveCategoryAsync(mappedCategory, userId);
        }

        return await ResolveCategoryAsync(normalizedHint, userId);
    }

    private async Task<Dictionary<string, Guid?>> GetMerchantCategoriesAsync(List<string> merchants, Guid userId)
    {
        if (merchants.Count == 0) return new Dictionary<string, Guid?>();

        try
        {
            _logger.LogDebug("Loading merchant categories for {Count} merchants", merchants.Count);

            var normalizedMerchants = merchants
                .Select(NormalizeMerchantForMapping)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lookupMerchants = merchants
                .Concat(normalizedMerchants)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Query merchant-category mappings for the given merchants using LINQ
            var mappings = await _context.UserMerchantCategoryMappings
                .Where(m => m.UserId == userId && lookupMerchants.Contains(m.MerchantName))
                .OrderBy(m => m.MerchantName)
                .ThenByDescending(m => m.ConfidenceScore)
                .ToListAsync();

            var result = new Dictionary<string, Guid?>();

            // Group by merchant and take the highest confidence mapping for each
            var groupedMappings = mappings
                .GroupBy(m => m.MerchantName)
                .ToDictionary(g => g.Key, g => (Guid?)g.First().CategoryId);

            // Add all merchants to result (null for those without mappings)
            foreach (var merchant in merchants)
            {
                var normalizedMerchant = NormalizeMerchantForMapping(merchant);
                if (groupedMappings.TryGetValue(merchant, out var categoryId) ||
                    groupedMappings.TryGetValue(normalizedMerchant, out categoryId))
                {
                    result[merchant] = categoryId;
                }
                else
                {
                    result[merchant] = null;
                }
            }

            _logger.LogDebug("Loaded {Count} merchant category mappings", groupedMappings.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading merchant categories for {Count} merchants", merchants.Count);
            return merchants.ToDictionary(m => m, _ => (Guid?)null);
        }
    }

    private async Task<Guid?> GetCategoryByNameAsync(string categoryName, Guid userId)
    {
        _logger.LogDebug("Looking for category '{CategoryName}' for user {UserId}", categoryName, userId);

        var category = await _context.Categories
            .Where(c => c.Name == categoryName && c.UserId == userId)
            .FirstOrDefaultAsync();

        if (category == null)
        {
            _logger.LogDebug("Category '{CategoryName}' not found for user {UserId}, attempting to create", categoryName, userId);

            // Try to create a default category for this user
            try
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = categoryName,
                    Type = CategoryType.Expense,
                    IsSystem = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new category '{CategoryName}' for user {UserId} with ID {CategoryId}", categoryName, userId, category.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create category '{CategoryName}' for user {UserId}", categoryName, userId);
                return null;
            }
        }
        else
        {
            _logger.LogDebug("Found existing category '{CategoryName}' for user {UserId} with ID {CategoryId}", categoryName, userId, category.Id);
        }

        return category?.Id;
    }

    private string GenerateCacheKey(string merchant, string? description, decimal amount, Guid userId, string? parsedCategoryHint = null)
    {
        var normalizedMerchant = NormalizeMerchantForMapping(merchant);
        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : NormalizeMerchantForMapping(description);
        var normalizedParsedHint = string.IsNullOrWhiteSpace(parsedCategoryHint)
            ? string.Empty
            : NormalizeMerchantForMapping(parsedCategoryHint);
        return $"{_categoryMappingCachePrefix}{userId}:{normalizedMerchant}:{normalizedDescription}:{amount:F2}:{normalizedParsedHint}";
    }

    private static string BuildLookupKey(string merchant, string? description, decimal amount, string? parsedCategoryHint)
        => $"{merchant}|{description}|{amount}|{parsedCategoryHint}";

    private static bool IsAiFallbackEnabled()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        var normalizedKey = apiKey.Trim();
        if (normalizedKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            return false;

        return normalizedKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid?> GetFallbackUncategorizedAsync(string merchant, Guid userId)
    {
        _logger.LogDebug("Falling back to 'Uncategorized' for {Merchant}", merchant);
        var uncategorized = await GetCategoryByNameAsync("Uncategorized", userId);
        if (uncategorized.HasValue)
        {
            _logger.LogDebug("Default assignment successful for {Merchant}: {CategoryId}", merchant, uncategorized);
        }
        else
        {
            _logger.LogWarning("Failed to get 'Uncategorized' category for user {UserId}", userId);
        }

        return uncategorized;
    }

    public async Task<Dictionary<string, object>> GetAssignmentStatsAsync()
    {
        var total = _cacheHits + _wellKnownMappings + _hfLookupMappings + _ruleMappings + _globalMappings + _merchantMappings + _aiFallbacks + _parsedFallbackMappings;
        return new Dictionary<string, object>
        {
            ["cache_hits"] = _cacheHits,
            ["well_known_mappings"] = _wellKnownMappings,
            ["hf_lookup_mappings"] = _hfLookupMappings,
            ["rule_mappings"] = _ruleMappings,
            ["global_mappings"] = _globalMappings,
            ["merchant_mappings"] = _merchantMappings,
            ["ai_fallbacks"] = _aiFallbacks,
            ["parsed_fallback_mappings"] = _parsedFallbackMappings,
            ["cache_efficiency"] = _cacheHits > 0 ? (double)_cacheHits / total : 0,
            ["total_assignments"] = total
        };
    }

    private static readonly Dictionary<string, string> ParsedCategoryFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Food & Drink"] = "Food & Dining",
        ["Food and Drink"] = "Food & Dining",
        ["Gas"] = "Transportation",
        ["Health & Wellness"] = "Healthcare",
        ["Health and Wellness"] = "Healthcare",
        ["Travel"] = "Travel",
        ["Personal"] = "Personal Care",
        ["Professional Services"] = "Business",
        ["Shopping"] = "Shopping",
        ["Entertainment"] = "Entertainment",
        ["Bills & Utilities"] = "Bills & Utilities",
        ["Bills and Utilities"] = "Bills & Utilities"
    };
}
