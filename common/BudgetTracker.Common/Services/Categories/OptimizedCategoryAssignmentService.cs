using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using System.Text.Json;

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

    // Performance counters
    private long _cacheHits = 0;
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

        // 2. Try rule-based assignment
        var ruleCategory = await TryRuleBasedAssignment(merchant, description, amount, userId);
        if (ruleCategory.HasValue)
        {
            Interlocked.Increment(ref _ruleMappings);
            _memoryCache.Set(cacheKey, ruleCategory.Value, _memoryCacheExpiry);
            return ruleCategory.Value;
        }

        // 3. Try merchant-based assignment (learn from previous assignments)
        var merchantCategory = await TryMerchantBasedAssignment(merchant, userId);
        if (merchantCategory.HasValue)
        {
            Interlocked.Increment(ref _merchantMappings);
            _memoryCache.Set(cacheKey, merchantCategory.Value, _memoryCacheExpiry);
            return merchantCategory.Value;
        }

        // 4. Default/AI fallback (placeholder for now)
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

        // 2. Batch load merchant categories for uncached transactions
        var merchants = uncachedTransactions.Select(t => t.merchant).Distinct().ToList();
        var merchantCategories = await GetMerchantCategoriesAsync(merchants, userId);

        // 3. Process uncached transactions
        foreach (var (lookupKey, merchant, description, amount) in uncachedTransactions)
        {
            Guid? categoryId = null;
            _logger.LogDebug("Processing uncached transaction: {LookupKey} (merchant: {Merchant})", lookupKey, merchant);

            // Try rule-based first
            categoryId = await TryRuleBasedAssignment(merchant, description, amount, userId);
            if (categoryId.HasValue)
            {
                Interlocked.Increment(ref _ruleMappings);
                _logger.LogDebug("Rule-based assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                
                // Learn from rule-based assignment
                await LearnFromAssignmentAsync(merchant, description, amount, categoryId.Value, userId);
            }
            else if (merchantCategories.TryGetValue(merchant, out var merchantCat) && merchantCat.HasValue)
            {
                categoryId = merchantCat;
                Interlocked.Increment(ref _merchantMappings);
                _logger.LogDebug("Merchant-based assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                
                // Learn from merchant-based assignment (reinforce existing mapping)
                await LearnFromAssignmentAsync(merchant, description, amount, categoryId.Value, userId);
            }
            else
            {
                // Fallback assignment
                categoryId = await TryDefaultAssignment(merchant, description, amount, userId);
                if (categoryId.HasValue)
                {
                    Interlocked.Increment(ref _aiFallbacks);
                    _logger.LogDebug("Default assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
                    
                    // Learning already happens in TryDefaultAssignment for AI fallback
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

        // Data-driven: keywords loaded from MerchantCategoryKeywords.json (MCC-based, extensible)
        var matchedCategory = MerchantCategoryKeywordLoader.MatchCategory(merchantLower, descriptionLower, amount);
        if (matchedCategory != null)
        {
            var category = await ResolveCategoryAsync(matchedCategory, userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                _logger.LogInformation("Assigned {Merchant} to {Category} (keyword match)", merchant, matchedCategory);
                return category.Value;
            }
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
Food & Dining, Transportation, Shopping, Entertainment, Bills & Utilities, Healthcare, Travel, Education, 
Personal Care, Home & Garden, Business, Investments, Income, Transfer, Uncategorized.

Transaction: {merchant} - {description} - Amount: ${amount:F2}

Return only the category name:";

                var aiCategory = await aiAnalyzer.CategorizeTransactionAsync(prompt);
                if (!string.IsNullOrEmpty(aiCategory))
                {
                    // Find category by name
                    var category = await GetCategoryByNameAsync(aiCategory, userId);
                    if (category.HasValue)
                    {
                        _logger.LogDebug("AI assignment successful for {Merchant}: {Category}", merchant, aiCategory);
                        
                        return category.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI fallback failed for {Merchant}, falling back to Uncategorized", merchant);
        }
        
        // Fallback to Uncategorized
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
        return new Dictionary<string, object>
        {
            ["cache_hits"] = _cacheHits,
            ["rule_mappings"] = _ruleMappings,
            ["merchant_mappings"] = _merchantMappings,
            ["ai_fallbacks"] = _aiFallbacks,
            ["cache_efficiency"] = _cacheHits > 0 ? (double)_cacheHits / (_cacheHits + _ruleMappings + _merchantMappings + _aiFallbacks) : 0,
            ["total_assignments"] = _cacheHits + _ruleMappings + _merchantMappings + _aiFallbacks
        };
    }
}