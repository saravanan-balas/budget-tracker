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
        ILogger<OptimizedCategoryAssignmentService> logger)
    {
        _context = context;
        _memoryCache = memoryCache;
        _logger = logger;
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
            }
            else if (merchantCategories.TryGetValue(merchant, out var merchantCat))
            {
                categoryId = merchantCat;
                Interlocked.Increment(ref _merchantMappings);
                _logger.LogDebug("Merchant-based assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
            }
            else
            {
                // Fallback assignment
                categoryId = await TryDefaultAssignment(merchant, description, amount, userId);
                if (categoryId.HasValue)
                {
                    Interlocked.Increment(ref _aiFallbacks);
                    _logger.LogDebug("Default assignment successful for {Merchant}: {CategoryId}", merchant, categoryId);
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
            // Store merchant-category mapping for future use
            var existingMapping = await _context.Database.SqlQuery<int>($@"
                SELECT COUNT(*) 
                FROM ""UserMerchantCategoryMappings"" 
                WHERE ""UserId"" = {userId} 
                AND ""MerchantName"" = {merchant} 
                AND ""CategoryId"" = {categoryId}")
                .FirstOrDefaultAsync();

            if (existingMapping == 0)
            {
                // Create the mapping
                await _context.Database.ExecuteSqlAsync($@"
                    INSERT INTO ""UserMerchantCategoryMappings"" 
                    (""Id"", ""UserId"", ""MerchantName"", ""CategoryId"", ""ConfidenceScore"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES ({Guid.NewGuid()}, {userId}, {merchant}, {categoryId}, 1.0, {DateTime.UtcNow}, {DateTime.UtcNow})
                    ON CONFLICT (""UserId"", ""MerchantName"") 
                    DO UPDATE SET 
                        ""CategoryId"" = {categoryId},
                        ""ConfidenceScore"" = ""UserMerchantCategoryMappings"".""ConfidenceScore"" + 0.1,
                        ""UpdatedAt"" = {DateTime.UtcNow}");
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

        // Common categorization rules
        var merchantLower = merchant.ToLowerInvariant();
        var descriptionLower = description?.ToLowerInvariant() ?? "";

        _logger.LogInformation("Applying rule-based categorization for merchant: {Merchant}", merchant);

        // Grocery stores
        if (IsGroceryStore(merchantLower) || descriptionLower.Contains("grocery"))
        {
            _logger.LogInformation("Merchant {Merchant} matched grocery store rule", merchant);
            var category = await GetCategoryByNameAsync("Groceries", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                _logger.LogInformation("Assigned {Merchant} to Groceries category", merchant);
                return category.Value;
            }
        }

        // Gas stations
        if (IsGasStation(merchantLower) || descriptionLower.Contains("fuel") || descriptionLower.Contains("gas"))
        {
            var category = await GetCategoryByNameAsync("Gas", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }

        // Restaurants
        if (IsRestaurant(merchantLower) || descriptionLower.Contains("restaurant") || descriptionLower.Contains("cafe"))
        {
            var category = await GetCategoryByNameAsync("Dining Out", userId) ?? 
                           await GetCategoryByNameAsync("Food & Dining", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }
        
        // Hotels and Lodging
        if (IsHotel(merchantLower) || descriptionLower.Contains("hotel") || descriptionLower.Contains("inn") || 
            descriptionLower.Contains("lodge") || descriptionLower.Contains("motel"))
        {
            var category = await GetCategoryByNameAsync("Hotel", userId) ?? 
                           await GetCategoryByNameAsync("Travel", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }
        
        // Car Rental and Transportation
        if (IsCarRental(merchantLower) || IsTransportation(merchantLower) || 
            descriptionLower.Contains("uber") || descriptionLower.Contains("lyft") || 
            descriptionLower.Contains("taxi") || descriptionLower.Contains("transit"))
        {
            var category = await GetCategoryByNameAsync("Transportation", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }
        
        // Pet Care
        if (IsPetCare(merchantLower) || descriptionLower.Contains("animal") || 
            descriptionLower.Contains("veterinar") || descriptionLower.Contains("pet"))
        {
            var category = await GetCategoryByNameAsync("Pet Care", userId) ?? 
                           await GetCategoryByNameAsync("Pets", userId) ??
                           await GetCategoryByNameAsync("Personal Care", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }
        
        // Online Shopping
        if (IsOnlineShopping(merchantLower) || descriptionLower.Contains("amazon") || 
            descriptionLower.Contains("ebay") || descriptionLower.Contains("etsy"))
        {
            var category = await GetCategoryByNameAsync("Shopping", userId) ?? 
                           await GetCategoryByNameAsync("Online Shopping", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }
        
        // Subscriptions and Software
        if (IsSubscription(merchantLower) || descriptionLower.Contains("subscription") || 
            descriptionLower.Contains("software"))
        {
            var category = await GetCategoryByNameAsync("Subscriptions", userId) ?? 
                           await GetCategoryByNameAsync("Software", userId) ??
                           await GetCategoryByNameAsync("Technology", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }
        
        // Alcohol and Liquor stores
        if (IsLiquorStore(merchantLower) || descriptionLower.Contains("liquor") || 
            descriptionLower.Contains("wine") || descriptionLower.Contains("beer"))
        {
            var category = await GetCategoryByNameAsync("Alcohol", userId) ?? 
                           await GetCategoryByNameAsync("Beverages", userId) ??
                           await GetCategoryByNameAsync("Food & Dining", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }

        // ATM/Bank fees
        if (merchantLower.Contains("atm") || merchantLower.Contains("fee") || descriptionLower.Contains("fee"))
        {
            var category = await GetCategoryByNameAsync("Bank Fees", userId);
            if (category.HasValue)
            {
                _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                return category.Value;
            }
        }

        // Income (positive amounts)
        if (amount > 0)
        {
            if (merchantLower.Contains("salary") || merchantLower.Contains("payroll") || descriptionLower.Contains("salary"))
            {
                var category = await GetCategoryByNameAsync("Salary", userId);
                if (category.HasValue)
                {
                    _memoryCache.Set(ruleKey, category.Value, _memoryCacheExpiry);
                    return category.Value;
                }
            }
        }

        return null;
    }

    private async Task<Guid?> TryMerchantBasedAssignment(string merchant, Guid userId)
    {
        var merchantCacheKey = $"{_merchantCategoryCachePrefix}{userId}:{merchant}";
        
        if (_memoryCache.TryGetValue(merchantCacheKey, out Guid? cachedMerchantCategory))
        {
            return cachedMerchantCategory;
        }

        // Look up learned merchant-category mappings
        var mapping = await _context.Database.SqlQuery<Guid?>($@"
            SELECT ""CategoryId""
            FROM ""UserMerchantCategoryMappings""
            WHERE ""UserId"" = {userId} AND ""MerchantName"" = {merchant}
            ORDER BY ""ConfidenceScore"" DESC, ""UpdatedAt"" DESC
            LIMIT 1")
            .FirstOrDefaultAsync();

        if (mapping.HasValue)
        {
            _memoryCache.Set(merchantCacheKey, mapping.Value, _memoryCacheExpiry);
        }

        return mapping;
    }

    private async Task<Guid?> TryDefaultAssignment(string merchant, string? description, decimal amount, Guid userId)
    {
        // For now, assign to "Uncategorized" or most common category
        // This could be enhanced with AI in the future
        _logger.LogDebug("Attempting default assignment for {Merchant} to 'Uncategorized' category", merchant);
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
        var merchantParams = string.Join(",", merchants.Select((_, i) => $"${i + 1}"));
        var parameters = new object[] { userId }.Concat(merchants.Cast<object>()).ToArray();
        
        // This would need to be implemented with Entity Framework properly
        // For now, return empty dictionary
        return new Dictionary<string, Guid?>();
    }

    private async Task<Guid?> GetCategoryByNameAsync(string categoryName, Guid userId)
    {
        _logger.LogDebug("Looking for category '{CategoryName}' for user {UserId}", categoryName, userId);
        
        var category = await _context.Categories
            .Where(c => c.Name == categoryName && (c.UserId == userId || c.IsSystem))
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

    // Rule-based categorization helpers
    private static bool IsGroceryStore(string merchant)
    {
        var groceryKeywords = new[] { "walmart", "kroger", "safeway", "publix", "whole foods", "trader joe", "costco", "target", "aldi" };
        return groceryKeywords.Any(keyword => merchant.Contains(keyword));
    }

    private static bool IsGasStation(string merchant)
    {
        var gasKeywords = new[] { "shell", "exxon", "bp", "chevron", "mobil", "texaco", "citgo", "valero", "marathon", "speedway" };
        return gasKeywords.Any(keyword => merchant.Contains(keyword));
    }

    private static bool IsRestaurant(string merchant)
    {
        var restaurantKeywords = new[] { "mcdonald", "burger", "pizza", "starbucks", "subway", "taco", "kfc", 
            "wendy", "domino", "restaurant", "chipotle", "panera", "dunkin", "diner", "grill", "bakery", 
            "coffee", "doordash", "grubhub", "uber eats", "postmates" };
        return restaurantKeywords.Any(keyword => merchant.Contains(keyword));
    }
    
    private static bool IsHotel(string merchant)
    {
        var hotelKeywords = new[] { "hotel", "inn", "suites", "marriott", "hilton", "hyatt", "sheraton", 
            "holiday inn", "la quinta", "baymont", "comfort inn", "best western", "radisson", "ramada",
            "days inn", "motel", "lodge", "resort", "airbnb", "vrbo" };
        return hotelKeywords.Any(keyword => merchant.Contains(keyword));
    }
    
    private static bool IsCarRental(string merchant)
    {
        var carRentalKeywords = new[] { "hertz", "avis", "budget", "enterprise", "national", "alamo", 
            "thrifty", "dollar", "zipcar", "turo", "rent-a-car", "rental car" };
        return carRentalKeywords.Any(keyword => merchant.Contains(keyword));
    }
    
    private static bool IsTransportation(string merchant)
    {
        var transportKeywords = new[] { "uber", "lyft", "taxi", "cab", "transit", "metro", "subway", 
            "train", "amtrak", "greyhound", "airlines", "airport", "parking" };
        return transportKeywords.Any(keyword => merchant.Contains(keyword));
    }
    
    private static bool IsPetCare(string merchant)
    {
        var petKeywords = new[] { "vet", "veterinary", "animal hospital", "pet", "petco", "petsmart", 
            "chewy", "animal clinic", "grooming", "kennel", "boarding" };
        return petKeywords.Any(keyword => merchant.Contains(keyword));
    }
    
    private static bool IsOnlineShopping(string merchant)
    {
        var shoppingKeywords = new[] { "amazon", "ebay", "etsy", "walmart.com", "target.com", "bestbuy", 
            "newegg", "alibaba", "wish", "shopify", "amzn.com" };
        return shoppingKeywords.Any(keyword => merchant.Contains(keyword));
    }
    
    private static bool IsSubscription(string merchant)
    {
        var subscriptionKeywords = new[] { "netflix", "spotify", "hulu", "disney", "youtube", "apple.com", 
            "microsoft", "adobe", "github", "openai", "chatgpt", "claude", "google", "dropbox", "icloud",
            "xbox", "playstation", "nintendo" };
        return subscriptionKeywords.Any(keyword => merchant.Contains(keyword));
    }
    
    private static bool IsLiquorStore(string merchant)
    {
        var liquorKeywords = new[] { "liquor", "wine", "spirits", "bevmo", "total wine", "abc store", 
            "beer", "brewery", "penguin liquor", "beverage" };
        return liquorKeywords.Any(keyword => merchant.Contains(keyword));
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