using Xunit;
using BudgetTracker.Common.Models;

namespace BudgetTracker.Categorization.Tests;

public class GlobalMappingTests : IClassFixture<CategorizationTestFixture>
{
    private readonly CategorizationTestFixture _fixture;

    public GlobalMappingTests(CategorizationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GlobalMapping_AssignsCorrectly_WhenMappingExists()
    {
        // Arrange - fixture already has ACME COFFEE SHOP → Food & Dining global mapping
        var merchant = "ACME COFFEE SHOP";
        var amount = 5.75m;

        // Act
        var categoryId = await _fixture.CategoryService.AssignCategoryAsync(merchant, null, amount, _fixture.TestUserId);

        // Assert
        Assert.NotNull(categoryId);
        var category = await _fixture.DbContext.Categories.FindAsync(categoryId.Value);
        Assert.NotNull(category);
        Assert.Equal("Food & Dining", category.Name);
    }

    [Fact]
    public async Task GlobalMapping_CreatesCategory_WhenUserDoesntHave()
    {
        // Arrange - create a second user who doesn't have the category yet
        var secondUserId = Guid.NewGuid();

        // Create global mapping for a category name that doesn't exist for second user
        _fixture.DbContext.GlobalMerchantCategoryMappings.Add(new GlobalMerchantCategoryMapping
        {
            Id = Guid.NewGuid(),
            MerchantName = "TEST MERCHANT",
            CategoryName = "Shopping",
            ConfirmationCount = 1,
            ConfidenceScore = 1.0m,
            Source = "AI",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _fixture.DbContext.SaveChangesAsync();

        // Add Shopping category for second user
        _fixture.DbContext.Categories.Add(new Category
        {
            Id = Guid.NewGuid(),
            UserId = secondUserId,
            Name = "Shopping",
            Type = CategoryType.Expense,
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var categoryId = await _fixture.CategoryService.AssignCategoryAsync("TEST MERCHANT", null, 100m, secondUserId);

        // Assert
        Assert.NotNull(categoryId);
        var category = await _fixture.DbContext.Categories.FindAsync(categoryId.Value);
        Assert.NotNull(category);
        Assert.Equal("Shopping", category.Name);
        Assert.Equal(secondUserId, category.UserId);
    }

    [Fact]
    public async Task GlobalMapping_DeferredToUserMapping_WhenUserOverrideExists()
    {
        // Arrange - use a unique merchant to avoid cache pollution from other tests
        var uniqueMerchant = "UNIQUE GLOBAL OVERRIDE TEST";

        // Create global mapping for this merchant
        _fixture.DbContext.GlobalMerchantCategoryMappings.Add(new GlobalMerchantCategoryMapping
        {
            Id = Guid.NewGuid(),
            MerchantName = uniqueMerchant,
            CategoryName = "Food & Dining",
            ConfirmationCount = 1,
            ConfidenceScore = 1.0m,
            Source = "AI",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Create user-specific mapping that conflicts with global
        var userCategory = _fixture.DbContext.Categories
            .First(c => c.UserId == _fixture.TestUserId && c.Name == "Shopping");

        _fixture.DbContext.UserMerchantCategoryMappings.Add(new UserMerchantCategoryMapping
        {
            Id = Guid.NewGuid(),
            UserId = _fixture.TestUserId,
            MerchantName = uniqueMerchant,
            CategoryId = userCategory.Id,
            ConfidenceScore = 1.0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var categoryId = await _fixture.CategoryService.AssignCategoryAsync(uniqueMerchant, null, 5.75m, _fixture.TestUserId);

        // Assert - should use user mapping (Shopping), not global mapping (Food & Dining)
        Assert.NotNull(categoryId);
        Assert.Equal(userCategory.Id, categoryId.Value);
        var category = await _fixture.DbContext.Categories.FindAsync(categoryId.Value);
        Assert.Equal("Shopping", category!.Name);
    }

    [Fact]
    public async Task LearnFromAssignment_UpdatesGlobalTable_ForAISource()
    {
        // Arrange
        var merchant = "NEW AI MERCHANT";
        var category = _fixture.DbContext.Categories
            .First(c => c.UserId == _fixture.TestUserId && c.Name == "Transportation");

        // Act
        await _fixture.CategoryService.LearnFromAssignmentAsync(merchant, null, 25m, category.Id, _fixture.TestUserId, "AI");

        // Assert
        var globalMapping = _fixture.DbContext.GlobalMerchantCategoryMappings
            .FirstOrDefault(m => m.MerchantName == merchant);
        Assert.NotNull(globalMapping);
        Assert.Equal("Transportation", globalMapping.CategoryName);
        Assert.Equal("AI", globalMapping.Source);
        Assert.Equal(1, globalMapping.ConfirmationCount);
    }

    [Fact]
    public async Task LearnFromAssignment_SkipsGlobal_ForUserSource()
    {
        // Arrange
        var merchant = "USER OVERRIDE MERCHANT";
        var category = _fixture.DbContext.Categories
            .First(c => c.UserId == _fixture.TestUserId && c.Name == "Entertainment");

        // Act
        await _fixture.CategoryService.LearnFromAssignmentAsync(merchant, null, 50m, category.Id, _fixture.TestUserId, "User");

        // Assert - should NOT create global mapping for user source
        var globalMapping = _fixture.DbContext.GlobalMerchantCategoryMappings
            .FirstOrDefault(m => m.MerchantName == merchant);
        Assert.Null(globalMapping);

        // But should create user mapping
        var userMapping = _fixture.DbContext.UserMerchantCategoryMappings
            .FirstOrDefault(m => m.UserId == _fixture.TestUserId && m.MerchantName == merchant);
        Assert.NotNull(userMapping);
    }

    [Fact]
    public async Task GlobalMapping_IncreasesConfidence_OnMultipleConfirmations()
    {
        // Arrange
        var merchant = "CONFIDENCE TEST MERCHANT";
        var category = _fixture.DbContext.Categories
            .First(c => c.UserId == _fixture.TestUserId && c.Name == "Groceries");

        // Act - learn from AI assignment multiple times (only AI results create global mappings)
        await _fixture.CategoryService.LearnFromAssignmentAsync(merchant, null, 30m, category.Id, _fixture.TestUserId, "AI");
        await _fixture.CategoryService.LearnFromAssignmentAsync(merchant, null, 35m, category.Id, _fixture.TestUserId, "AI");
        await _fixture.CategoryService.LearnFromAssignmentAsync(merchant, null, 40m, category.Id, _fixture.TestUserId, "AI");

        // Assert - confidence should have increased
        // Flush any pending changes to ensure data is visible
        await _fixture.DbContext.SaveChangesAsync();

        // List all global mappings for debugging
        var allGlobalMappings = _fixture.DbContext.GlobalMerchantCategoryMappings.ToList();
        var globalMapping = allGlobalMappings.FirstOrDefault(m => m.MerchantName.Contains("CONFIDENCE"));

        Assert.NotNull(globalMapping);
        Assert.Equal("Groceries", globalMapping.CategoryName);
        Assert.Equal(3, globalMapping.ConfirmationCount);
        Assert.Equal(1.2m, globalMapping.ConfidenceScore); // 1.0 + 0.1 + 0.1
    }

}
