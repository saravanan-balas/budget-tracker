using BudgetTracker.Common.Services.Categories;
using Xunit;

namespace BudgetTracker.Categorization.Tests;

/// <summary>
/// Tests that the curated MCC keyword list does NOT produce false positives.
/// The old auto-generated MCC keywords had single-word entries like "air", "gas", "car",
/// "delta", "united", "express", "budget", "hotel", "inn" etc. that matched nearly everything.
/// These tests ensure those false positives are eliminated.
/// </summary>
public class MerchantCategoryKeywordLoaderTests
{
    // ========================
    // FALSE POSITIVE REGRESSION TESTS
    // These used to incorrectly match with the old MCC keywords
    // ========================

    [Theory]
    [InlineData("DELTA DENTAL", "DELTA DENTAL PREMIUM")]
    [InlineData("AIR FRESHENER CO", "")]
    [InlineData("EXPRESS SCRIPTS", "EXPRESS SCRIPTS PHARMACY")]
    [InlineData("UNITED HEALTHCARE", "UNITED HEALTHCARE PREMIUM")]
    [InlineData("AMERICAN EXPRESS", "AMERICAN EXPRESS PAYMENT")]
    [InlineData("ISLAND PACIFIC SUPERMARKET", "")]
    [InlineData("AUTOZONE", "AUTOZONE #1234")]
    [InlineData("NEW YORK LIFE", "")]
    [InlineData("BUDGET BLINDS", "")]
    [InlineData("GOLDEN CORRAL", "")]
    public void MatchCategory_ShouldNotReturnTransportation(string merchant, string description)
    {
        var result = MerchantCategoryKeywordLoader.MatchCategory(
            merchant.ToLowerInvariant(), description.ToLowerInvariant(), -50.00m);
        Assert.NotEqual("Transportation", result);
    }

    [Theory]
    [InlineData("BUDGET BLINDS", "")]
    [InlineData("GOLDEN CORRAL", "GOLDEN CORRAL #1234")]
    [InlineData("ROYAL FARMS", "ROYAL FARMS #123")]
    [InlineData("CITY MARKET", "CITY MARKET #234")]
    [InlineData("BEST BUY", "BEST BUY #1234")]
    [InlineData("HOLIDAY INN EXPRESS", "")]
    public void MatchCategory_ShouldNotReturnTravel(string merchant, string description)
    {
        var result = MerchantCategoryKeywordLoader.MatchCategory(
            merchant.ToLowerInvariant(), description.ToLowerInvariant(), -50.00m);
        Assert.NotEqual("Travel", result);
    }

    // ========================
    // POSITIVE TESTS — keywords that SHOULD match
    // ========================

    [Theory]
    [InlineData("local grocery store", "", "Groceries")]
    [InlineData("main street supermarket", "", "Groceries")]
    public void MatchCategory_GroceryKeywords_ShouldMatch(string merchant, string description, string expected)
    {
        var result = MerchantCategoryKeywordLoader.MatchCategory(merchant, description, -50.00m);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("joes restaurant", "", "Dining Out")]
    [InlineData("pizza restaurant delivery", "", "Dining Out")]
    [InlineData("fast food place", "", "Dining Out")]
    public void MatchCategory_DiningKeywords_ShouldMatch(string merchant, string description, string expected)
    {
        var result = MerchantCategoryKeywordLoader.MatchCategory(merchant, description, -50.00m);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("downtown pharmacy", "", "Healthcare")]
    [InlineData("city hospital", "", "Healthcare")]
    [InlineData("dental lab", "", "Healthcare")]
    public void MatchCategory_HealthcareKeywords_ShouldMatch(string merchant, string description, string expected)
    {
        var result = MerchantCategoryKeywordLoader.MatchCategory(merchant, description, -50.00m);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MatchCategory_SalaryOnlyForPositiveAmounts()
    {
        var result = MerchantCategoryKeywordLoader.MatchCategory("direct deposit payroll", "", 5000m);
        Assert.Equal("Salary", result);

        // Negative amount should NOT match Salary
        result = MerchantCategoryKeywordLoader.MatchCategory("direct deposit payroll", "", -100m);
        Assert.Null(result);
    }
}
