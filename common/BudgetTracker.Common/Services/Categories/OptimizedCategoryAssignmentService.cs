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
    private long _merchantMappings = 0;
    private long _aiFallbacks = 0;

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

        // 2. Try well-known merchant heuristics (cheap, deterministic, high-precision)
        var wellKnownCategory = await TryWellKnownMerchantAssignment(merchant, description, userId);
        if (wellKnownCategory.HasValue)
        {
            Interlocked.Increment(ref _wellKnownMappings);
            _memoryCache.Set(cacheKey, wellKnownCategory.Value, _memoryCacheExpiry);
            return wellKnownCategory.Value;
        }

        // 3. Try HF transaction dataset lookup (free, large dataset)
        var hfCategory = await TryHfDatasetAssignment(merchant, description, userId);
        if (hfCategory.HasValue)
        {
            Interlocked.Increment(ref _hfLookupMappings);
            _memoryCache.Set(cacheKey, hfCategory.Value, _memoryCacheExpiry);
            return hfCategory.Value;
        }

        // 4. Try curated MCC keyword rules (broad patterns, safety net)
        var ruleCategory = await TryRuleBasedAssignment(merchant, description, amount, userId);
        if (ruleCategory.HasValue)
        {
            Interlocked.Increment(ref _ruleMappings);
            _memoryCache.Set(cacheKey, ruleCategory.Value, _memoryCacheExpiry);
            return ruleCategory.Value;
        }

        // 5. Try learned merchant mappings (DB lookup, user-specific)
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

        // 6. AI fallback (most expensive - result is persisted to avoid repeat calls)
        var defaultCategory = await TryDefaultAssignment(merchant, description, amount, userId);
        if (defaultCategory.HasValue)
        {
            Interlocked.Increment(ref _aiFallbacks);
            _memoryCache.Set(cacheKey, defaultCategory.Value, _memoryCacheExpiry);
        }

        return defaultCategory;
    }

    public async Task<Dictionary<string, Guid?>> BatchAssignCategoriesAsync(
        List<(string merchant, string? description, decimal amount)> transactions,
        Guid userId)
    {
        var results = new Dictionary<string, Guid?>();
        var uncachedTransactions = new List<(string key, string merchant, string? description, decimal amount)>();

        // 1. Check cache for all transactions first
        foreach (var (merchant, description, amount) in transactions)
        {
            var cacheKey = GenerateCacheKey(merchant, description, amount, userId);
            var lookupKey = $"{merchant}|{description}|{amount}";

            if (_memoryCache.TryGetValue(cacheKey, out Guid? cachedCategoryId))
            {
                results[lookupKey] = cachedCategoryId;
                Interlocked.Increment(ref _cacheHits);
            }
            else
            {
                uncachedTransactions.Add((lookupKey, merchant, description, amount));
            }
        }

        if (uncachedTransactions.Count == 0)
        {
            return results;
        }

        // 2. Batch load merchant categories for uncached transactions (skipped when SkipMerchantMappings)
        var merchants = uncachedTransactions.Select(t => t.merchant).Distinct().ToList();
        var merchantCategories = SkipMerchantMappings
            ? new Dictionary<string, Guid?>()
            : await GetMerchantCategoriesAsync(merchants, userId);

        // 3. Process uncached transactions (pipeline: well-known -> HF -> MCC -> learned -> AI)
        var batchWellKnown = 0;
        var batchHf = 0;
        var batchMcc = 0;
        var batchMerchant = 0;
        var batchAi = 0;
        foreach (var (lookupKey, merchant, description, amount) in uncachedTransactions)
        {
            Guid? categoryId = null;
            _logger.LogDebug("Processing uncached transaction: {LookupKey} (merchant: {Merchant})", lookupKey, merchant);

            // Try well-known merchant heuristics first
            categoryId = await TryWellKnownMerchantAssignment(merchant, description, userId);
            if (categoryId.HasValue)
            {
                Interlocked.Increment(ref _wellKnownMappings);
                batchWellKnown++;
                _logger.LogDebug("Well-known merchant assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                await LearnFromAssignmentAsync(merchant, description, amount, categoryId.Value, userId);
            }
            else if ((categoryId = await TryHfDatasetAssignment(merchant, description, userId)).HasValue)
            {
                Interlocked.Increment(ref _hfLookupMappings);
                batchHf++;
                _logger.LogDebug("Hugging Face dataset assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                await LearnFromAssignmentAsync(merchant, description, amount, categoryId.Value, userId);
            }
            else if ((categoryId = await TryRuleBasedAssignment(merchant, description, amount, userId)).HasValue)
            {
                Interlocked.Increment(ref _ruleMappings);
                batchMcc++;
                _logger.LogDebug("Rule-based (MCC) assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                await LearnFromAssignmentAsync(merchant, description, amount, categoryId.Value, userId);
            }
            else if (merchantCategories.TryGetValue(merchant, out var merchantCat) && merchantCat.HasValue)
            {
                categoryId = merchantCat;
                Interlocked.Increment(ref _merchantMappings);
                batchMerchant++;
                _logger.LogDebug("Merchant-based assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                await LearnFromAssignmentAsync(merchant, description, amount, categoryId.Value, userId);
            }
            else
            {
                // AI fallback (persists result internally to avoid repeat calls)
                categoryId = await TryDefaultAssignment(merchant, description, amount, userId);
                if (categoryId.HasValue)
                {
                    Interlocked.Increment(ref _aiFallbacks);
                    batchAi++;
                    _logger.LogDebug("AI/default assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                }
                else
                {
                    _logger.LogWarning("All categorization methods failed for {Merchant}", merchant);
                }
            }

            results[lookupKey] = categoryId;
            _logger.LogDebug("Storing result for key '{LookupKey}': {CategoryId}", lookupKey, categoryId?.ToString() ?? "null");

            // Cache the result
            var cacheKey = GenerateCacheKey(merchant, description, amount, userId);
            if (categoryId.HasValue)
            {
                _memoryCache.Set(cacheKey, categoryId.Value, _memoryCacheExpiry);
            }
        }

        _logger.LogInformation(
            "Categorization batch complete: {WellKnownCount} from well-known merchants, {HfCount} from Hugging Face dataset, {MccCount} from MCC keywords, {MerchantCount} from learned mappings, {AiCount} from AI/fallback (processed {Total} uncached)",
            batchWellKnown, batchHf, batchMcc, batchMerchant, batchAi, uncachedTransactions.Count);

        return results;
    }

    public async Task LearnFromAssignmentAsync(string merchant, string? description, decimal amount, Guid categoryId, Guid userId)
    {
        try
        {
            // Check if mapping already exists using LINQ
            var existingMapping = await _context.UserMerchantCategoryMappings
                .FirstOrDefaultAsync(m => m.UserId == userId && m.MerchantName == merchant);

            if (existingMapping == null)
            {
                // Create the mapping
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
                await _context.SaveChangesAsync();
            }
            else
            {
                // Update existing mapping
                existingMapping.CategoryId = categoryId;
                existingMapping.ConfidenceScore += 0.1m;
                existingMapping.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

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

    private async Task<Guid?> TryMerchantBasedAssignment(string merchant, Guid userId)
    {
        var merchantCacheKey = $"{_merchantCategoryCachePrefix}{userId}:{merchant}";

        if (_memoryCache.TryGetValue(merchantCacheKey, out Guid? cachedMerchantCategory))
        {
            return cachedMerchantCategory;
        }

        // Look up learned merchant-category mappings using LINQ
        var mappingEntity = await _context.UserMerchantCategoryMappings
            .Where(m => m.UserId == userId && m.MerchantName == merchant)
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

    private async Task<Guid?> TryDefaultAssignment(string merchant, string? description, decimal amount, Guid userId)
    {
        _logger.LogDebug("Attempting AI fallback assignment for {Merchant}", merchant);

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
            _logger.LogWarning(ex, "AI fallback failed for {Merchant}, falling back to Uncategorized", merchant);
        }

        // Fallback to Uncategorized (not persisted to learned mappings)
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

    private async Task<Dictionary<string, Guid?>> GetMerchantCategoriesAsync(List<string> merchants, Guid userId)
    {
        if (merchants.Count == 0) return new Dictionary<string, Guid?>();

        try
        {
            _logger.LogDebug("Loading merchant categories for {Count} merchants", merchants.Count);

            // Query merchant-category mappings for the given merchants using LINQ
            var mappings = await _context.UserMerchantCategoryMappings
                .Where(m => m.UserId == userId && merchants.Contains(m.MerchantName))
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
                result[merchant] = groupedMappings.TryGetValue(merchant, out var categoryId) ? categoryId : null;
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

    private string GenerateCacheKey(string merchant, string? description, decimal amount, Guid userId)
    {
        return $"{_categoryMappingCachePrefix}{userId}:{merchant}:{description}:{amount:F2}";
    }

    public async Task<Dictionary<string, object>> GetAssignmentStatsAsync()
    {
        var total = _cacheHits + _wellKnownMappings + _hfLookupMappings + _ruleMappings + _merchantMappings + _aiFallbacks;
        return new Dictionary<string, object>
        {
            ["cache_hits"] = _cacheHits,
            ["well_known_mappings"] = _wellKnownMappings,
            ["hf_lookup_mappings"] = _hfLookupMappings,
            ["rule_mappings"] = _ruleMappings,
            ["merchant_mappings"] = _merchantMappings,
            ["ai_fallbacks"] = _aiFallbacks,
            ["cache_efficiency"] = _cacheHits > 0 ? (double)_cacheHits / total : 0,
            ["total_assignments"] = total
        };
    }
}
