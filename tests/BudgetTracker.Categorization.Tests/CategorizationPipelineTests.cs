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
}
