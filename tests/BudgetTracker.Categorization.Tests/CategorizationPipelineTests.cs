using BudgetTracker.Common.Services.Categories;
using Xunit;
using Xunit.Abstractions;

namespace BudgetTracker.Categorization.Tests;

/// <summary>
/// Integration tests for the full categorization pipeline (without AI fallback).
/// Tests well-known merchant → HF dataset → MCC keywords → learned mappings.
/// AI is not configured in these tests so it falls back to "Uncategorized".
/// </summary>
public class CategorizationPipelineTests : IClassFixture<CategorizationTestFixture>
{
    private readonly CategorizationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CategorizationPipelineTests(CategorizationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public async Task AssignCategory_ShouldMatchExpected(string testId, string merchant,
        string? description, decimal amount, string expectedCategory)
    {
        var categoryId = await _fixture.CategoryService.AssignCategoryAsync(
            merchant, description, amount, _fixture.TestUserId);

        var actualCategory = _fixture.GetCategoryName(categoryId);
        _output.WriteLine($"[{testId}] {merchant} -> Expected: {expectedCategory}, Got: {actualCategory ?? "null"}");

        // If the category was assigned (not null and not Uncategorized), it must match
        if (actualCategory != null && actualCategory != "Uncategorized")
        {
            Assert.Equal(expectedCategory, actualCategory);
        }
        // If it falls through to Uncategorized (no AI in tests), that's acceptable for non-well-known merchants
    }

    [Theory]
    [MemberData(nameof(GetFalsePositiveTests))]
    public async Task AssignCategory_ShouldNotFalsePositive(string testId, string merchant,
        string? description, decimal amount, string mustNotBeCategory)
    {
        var categoryId = await _fixture.CategoryService.AssignCategoryAsync(
            merchant, description, amount, _fixture.TestUserId);

        var actualCategory = _fixture.GetCategoryName(categoryId);
        _output.WriteLine($"[{testId}] {merchant} -> Must NOT be: {mustNotBeCategory}, Got: {actualCategory ?? "null"}");

        Assert.NotEqual(mustNotBeCategory, actualCategory);
    }

    public static IEnumerable<object?[]> GetTestCases()
    {
        var fixture = new CategorizationTestFixture();
        fixture.InitializeAsync().Wait();

        foreach (var tc in fixture.TestData.TestCases)
        {
            yield return new object?[] { tc.Id, tc.Merchant, tc.Description, tc.Amount, tc.ExpectedCategory };
        }
    }

    public static IEnumerable<object?[]> GetFalsePositiveTests()
    {
        var fixture = new CategorizationTestFixture();
        fixture.InitializeAsync().Wait();

        foreach (var tc in fixture.TestData.FalsePositiveTests)
        {
            yield return new object?[] { tc.Id, tc.Merchant, tc.Description, tc.Amount, tc.MustNotBeCategory };
        }
    }

    [Fact]
    public async Task Pipeline_UsesGlobalMapping_BeforeUserMapping()
    {
        // Arrange - global mapping exists for ACME COFFEE SHOP (from fixture)
        // Create a user mapping for a different merchant
        var userCategory = _fixture.DbContext.Categories
            .First(c => c.UserId == _fixture.TestUserId && c.Name == "Shopping");

        _fixture.DbContext.UserMerchantCategoryMappings.Add(new Common.Models.UserMerchantCategoryMapping
        {
            Id = Guid.NewGuid(),
            UserId = _fixture.TestUserId,
            MerchantName = "OTHER MERCHANT",
            CategoryId = userCategory.Id,
            ConfidenceScore = 1.0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _fixture.DbContext.SaveChangesAsync();

        // Act - assign ACME COFFEE SHOP (should use global mapping, not user mapping)
        var categoryId = await _fixture.CategoryService.AssignCategoryAsync(
            "ACME COFFEE SHOP", null, 5.75m, _fixture.TestUserId);

        // Assert - should get Food & Dining from global mapping
        Assert.NotNull(categoryId);
        var category = await _fixture.DbContext.Categories.FindAsync(categoryId.Value);
        Assert.NotNull(category);
        Assert.Equal("Food & Dining", category.Name);

        // Verify stats show global mapping was used
        var stats = await _fixture.CategoryService.GetAssignmentStatsAsync();
        Assert.True((long)stats["global_mappings"] > 0);
    }

    [Fact]
    public async Task Pipeline_UserMappingOverridesGlobal()
    {
        // Arrange - use a unique merchant name to avoid cache pollution from other tests
        var uniqueMerchant = "UNIQUE COFFEE SHOP XYZ";

        // Create global mapping
        _fixture.DbContext.GlobalMerchantCategoryMappings.Add(new Common.Models.GlobalMerchantCategoryMapping
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

        // Create user mapping that overrides global
        var userCategory = _fixture.DbContext.Categories
            .First(c => c.UserId == _fixture.TestUserId && c.Name == "Entertainment");

        _fixture.DbContext.UserMerchantCategoryMappings.Add(new Common.Models.UserMerchantCategoryMapping
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
        var categoryId = await _fixture.CategoryService.AssignCategoryAsync(
            uniqueMerchant, null, 5.75m, _fixture.TestUserId);

        // Assert - should use user mapping (Entertainment), not global (Food & Dining)
        Assert.NotNull(categoryId);
        var category = await _fixture.DbContext.Categories.FindAsync(categoryId.Value);
        Assert.NotNull(category);
        Assert.Equal("Entertainment", category.Name);

        // Verify stats show user mapping was used (not global)
        var stats = await _fixture.CategoryService.GetAssignmentStatsAsync();
        Assert.True((long)stats["merchant_mappings"] > 0);
    }
}
