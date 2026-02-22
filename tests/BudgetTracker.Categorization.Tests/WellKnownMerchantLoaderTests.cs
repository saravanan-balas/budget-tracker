using BudgetTracker.Common.Services.Categories;
using Xunit;

namespace BudgetTracker.Categorization.Tests;

public class WellKnownMerchantLoaderTests
{
    [Theory]
    [InlineData("WALMART #2280 MOUNTAIN VIEW CA", null, "Groceries")]
    [InlineData("WM SUPERCENTER #1234", null, "Groceries")]
    [InlineData("WAL-MART STORE", null, "Groceries")]
    [InlineData("KROGER #366", null, "Groceries")]
    [InlineData("WHOLEFDS MKT 10234", null, "Groceries")]
    [InlineData("TRADER JOE'S #123", null, "Groceries")]
    [InlineData("ALDI #12345", null, "Groceries")]
    [InlineData("PUBLIX SUPER MARKETS", null, "Groceries")]
    [InlineData("SAMSCLUB #6657", null, "Groceries")]
    [InlineData("COSTCO WHSE #1087", null, "Groceries")]
    [InlineData("VANI FOODS", null, "Groceries")]
    public void TryMatch_Groceries_ReturnsGroceries(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UBER *EATS HELP.UBER.COM CA", null, "Food & Dining")]
    [InlineData("UBEREATS ORDER", null, "Food & Dining")]
    [InlineData("DD *DOORDASH CHIPOTLE", null, "Food & Dining")]
    [InlineData("STARBUCKS #12345", null, "Food & Dining")]
    [InlineData("MCDONALD'S F12345", null, "Food & Dining")]
    [InlineData("CHIPOTLE ONLINE", null, "Food & Dining")]
    [InlineData("CHICK-FIL-A #1234", null, "Food & Dining")]
    [InlineData("TACO BELL #1234", null, "Food & Dining")]
    [InlineData("PANERA BREAD", null, "Food & Dining")]
    [InlineData("DUNKIN #1234", null, "Food & Dining")]
    public void TryMatch_FoodAndDining_ReturnsFoodAndDining(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UBER *TRIP HELP.UBER.COM", null, "Transportation")]
    [InlineData("LYFT *RIDE", null, "Transportation")]
    [InlineData("SHELL OIL 12345", null, "Transportation")]
    [InlineData("CHEVRON 0012345", null, "Transportation")]
    [InlineData("PTC EZ PASS AUTO REPLENISH", null, "Transportation")]
    [InlineData("COSTCO GAS #1087", null, "Transportation")]
    public void TryMatch_Transportation_ReturnsTransportation(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("NETFLIX.COM", null, "Entertainment")]
    [InlineData("SPOTIFY USA", null, "Entertainment")]
    [InlineData("HULU", null, "Entertainment")]
    [InlineData("AUDIBLE US", null, "Entertainment")]
    [InlineData("YUPPTV USA INC", null, "Entertainment")]
    public void TryMatch_Entertainment_ReturnsEntertainment(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("AMZN MKTP US*1A2B3C4D5", null, "Shopping")]
    [InlineData("AMAZON.COM*1A2B3C", null, "Shopping")]
    [InlineData("TARGET 00012345", null, "Shopping")]
    [InlineData("THE HOME DEPOT #1234", null, "Shopping")]
    [InlineData("KATE SPADE OUTLET", null, "Shopping")]
    public void TryMatch_Shopping_ReturnsShopping(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("COMCAST CABLE COMM", null, "Bills & Utilities")]
    [InlineData("VERIZON WIRELESS", null, "Bills & Utilities")]
    [InlineData("OPENAI *CHATGPT SUBSCR", null, "Bills & Utilities")]
    [InlineData("SIMPLISAFE", null, "Bills & Utilities")]
    [InlineData("GITHUB", null, "Bills & Utilities")]
    public void TryMatch_BillsAndUtilities_ReturnsBillsAndUtilities(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("CVS/PHARMACY #1234", null, "Healthcare")]
    [InlineData("WALGREENS #8780", null, "Healthcare")]
    public void TryMatch_Healthcare_ReturnsHealthcare(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("VENMO PAYMENT", null, "Transfer")]
    [InlineData("ZELLE SEND", null, "Transfer")]
    [InlineData("PAYPAL *TRANSFER", null, "Transfer")]
    public void TryMatch_Transfer_ReturnsTransfer(string merchant, string? description, string expected)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, description);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryMatch_UberEatsVsUberTrip_CorrectlyDisambiguates()
    {
        // Uber Eats should be Food & Dining
        Assert.Equal("Food & Dining", WellKnownMerchantLoader.TryMatch("UBER *EATS", null));

        // Uber Trip should be Transportation (not Food & Dining)
        Assert.Equal("Transportation", WellKnownMerchantLoader.TryMatch("UBER *TRIP", null));
    }

    [Fact]
    public void TryMatch_CostcoWholesaleVsGas_CorrectlyDisambiguates()
    {
        // Costco Wholesale should be Groceries
        Assert.Equal("Groceries", WellKnownMerchantLoader.TryMatch("COSTCO WHSE #1087", null));

        // Costco Gas should be Transportation
        Assert.Equal("Transportation", WellKnownMerchantLoader.TryMatch("COSTCO GAS #1087", null));
    }

    [Theory]
    [InlineData("SOME RANDOM STORE")]
    [InlineData("XYZ CORP PAYMENT")]
    [InlineData("")]
    public void TryMatch_UnknownMerchant_ReturnsNull(string merchant)
    {
        var result = WellKnownMerchantLoader.TryMatch(merchant, null);
        Assert.Null(result);
    }

    [Fact]
    public void TryMatch_DescriptionAlsoSearched()
    {
        // When merchant is generic but description has a known pattern
        var result = WellKnownMerchantLoader.TryMatch("SQ *", "SQ *STARBUCKS STORE");
        Assert.Equal("Food & Dining", result);
    }

    [Fact]
    public void TryMatch_CaseInsensitive()
    {
        Assert.Equal("Groceries", WellKnownMerchantLoader.TryMatch("walmart", null));
        Assert.Equal("Groceries", WellKnownMerchantLoader.TryMatch("WALMART", null));
        Assert.Equal("Groceries", WellKnownMerchantLoader.TryMatch("Walmart", null));
    }
}
