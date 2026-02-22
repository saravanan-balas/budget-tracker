using BudgetTracker.Common.Services.Categories;
using Xunit;
using Xunit.Abstractions;

namespace BudgetTracker.Categorization.Tests;

/// <summary>
/// Generates an accuracy report for the categorization pipeline.
/// This is a single test that runs all fixture test cases and outputs metrics.
/// Run with: dotnet test --filter "FullyQualifiedName~CategorizationAccuracyReport" --logger "console;verbosity=detailed"
/// </summary>
public class CategorizationAccuracyReport : IClassFixture<CategorizationTestFixture>
{
    private readonly CategorizationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CategorizationAccuracyReport(CategorizationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GenerateReport()
    {
        var testCases = _fixture.TestData.TestCases;
        var falsePositiveTests = _fixture.TestData.FalsePositiveTests;

        var correct = 0;
        var incorrect = 0;
        var uncategorized = 0;
        var categoryResults = new Dictionary<string, (int correct, int incorrect, int uncategorized)>();
        var sourceDistribution = new Dictionary<string, int>
        {
            ["well_known"] = 0,
            ["hf_lookup"] = 0,
            ["mcc_keywords"] = 0,
            ["merchant_mapping"] = 0,
            ["ai_fallback"] = 0,
            ["uncategorized"] = 0
        };
        var failures = new List<string>();

        _output.WriteLine("=" + new string('=', 79));
        _output.WriteLine("CATEGORIZATION ACCURACY REPORT");
        _output.WriteLine("=" + new string('=', 79));
        _output.WriteLine("");

        // Run each test case
        foreach (var tc in testCases)
        {
            var categoryId = await _fixture.CategoryService.AssignCategoryAsync(
                tc.Merchant, tc.Description, tc.Amount, _fixture.TestUserId);

            var actualCategory = _fixture.GetCategoryName(categoryId);
            var expected = tc.ExpectedCategory;

            if (!categoryResults.ContainsKey(expected))
                categoryResults[expected] = (0, 0, 0);

            if (actualCategory == "Uncategorized" || actualCategory == null)
            {
                uncategorized++;
                var (c, i, u) = categoryResults[expected];
                categoryResults[expected] = (c, i, u + 1);
                sourceDistribution["uncategorized"]++;
                _output.WriteLine($"  ? {tc.Id,-40} Expected: {expected,-20} Got: {actualCategory ?? "null"}");
            }
            else if (actualCategory == expected)
            {
                correct++;
                var (c, i, u) = categoryResults[expected];
                categoryResults[expected] = (c + 1, i, u);
                _output.WriteLine($"  + {tc.Id,-40} Expected: {expected,-20} Got: {actualCategory}");
            }
            else
            {
                incorrect++;
                var (c, i, u) = categoryResults[expected];
                categoryResults[expected] = (c, i + 1, u);
                failures.Add($"{tc.Id}: expected '{expected}', got '{actualCategory}'");
                _output.WriteLine($"  X {tc.Id,-40} Expected: {expected,-20} Got: {actualCategory} *** WRONG ***");
            }
        }

        // False positive tests
        var fpPassed = 0;
        var fpFailed = 0;
        _output.WriteLine("");
        _output.WriteLine("-" + new string('-', 79));
        _output.WriteLine("FALSE POSITIVE TESTS");
        _output.WriteLine("-" + new string('-', 79));

        foreach (var fp in falsePositiveTests)
        {
            var categoryId = await _fixture.CategoryService.AssignCategoryAsync(
                fp.Merchant, fp.Description, fp.Amount, _fixture.TestUserId);

            var actualCategory = _fixture.GetCategoryName(categoryId);

            if (actualCategory == fp.MustNotBeCategory)
            {
                fpFailed++;
                _output.WriteLine($"  X {fp.Id,-40} Got: {actualCategory,-20} *** FALSE POSITIVE ***");
            }
            else
            {
                fpPassed++;
                _output.WriteLine($"  + {fp.Id,-40} Got: {actualCategory ?? "null",-20} (not {fp.MustNotBeCategory})");
            }
        }

        // Get pipeline stats
        var stats = await _fixture.CategoryService.GetAssignmentStatsAsync();

        // Summary
        var total = correct + incorrect + uncategorized;
        var categorizedTotal = correct + incorrect;

        _output.WriteLine("");
        _output.WriteLine("=" + new string('=', 79));
        _output.WriteLine("SUMMARY");
        _output.WriteLine("=" + new string('=', 79));
        _output.WriteLine($"  Total test cases:           {total}");
        _output.WriteLine($"  Correctly categorized:      {correct} ({(total > 0 ? 100.0 * correct / total : 0):F1}%)");
        _output.WriteLine($"  Incorrectly categorized:    {incorrect} ({(total > 0 ? 100.0 * incorrect / total : 0):F1}%)");
        _output.WriteLine($"  Fell through to Uncategorized: {uncategorized} ({(total > 0 ? 100.0 * uncategorized / total : 0):F1}%)");
        _output.WriteLine($"  Accuracy (of categorized):  {(categorizedTotal > 0 ? 100.0 * correct / categorizedTotal : 0):F1}%");
        _output.WriteLine("");
        _output.WriteLine($"  False positive tests:       {fpPassed + fpFailed}");
        _output.WriteLine($"  False positives passed:     {fpPassed}");
        _output.WriteLine($"  False positives FAILED:     {fpFailed}");
        _output.WriteLine("");
        _output.WriteLine("  Pipeline stats:");
        foreach (var (key, value) in stats)
        {
            _output.WriteLine($"    {key,-25}: {value}");
        }

        _output.WriteLine("");
        _output.WriteLine("  Per-category breakdown:");
        foreach (var (cat, (c, i, u)) in categoryResults.OrderByDescending(x => x.Value.correct + x.Value.incorrect + x.Value.uncategorized))
        {
            var catTotal = c + i + u;
            _output.WriteLine($"    {cat,-25}: {c}/{catTotal} correct ({(catTotal > 0 ? 100.0 * c / catTotal : 0):F0}%) | {i} wrong | {u} uncategorized");
        }

        if (failures.Count > 0)
        {
            _output.WriteLine("");
            _output.WriteLine("  FAILURES:");
            foreach (var f in failures)
                _output.WriteLine($"    - {f}");
        }

        _output.WriteLine("=" + new string('=', 79));

        // Assert no false positives
        Assert.Equal(0, fpFailed);

        // Assert at least 50% accuracy (reasonable baseline without AI)
        if (total > 0)
        {
            var accuracyPct = 100.0 * correct / total;
            Assert.True(accuracyPct >= 50, $"Overall accuracy {accuracyPct:F1}% is below 50% threshold");
        }
    }
}
