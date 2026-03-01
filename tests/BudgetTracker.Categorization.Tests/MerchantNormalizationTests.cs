using Xunit;
using System.Reflection;

namespace BudgetTracker.Categorization.Tests;

/// <summary>
/// Tests for merchant name normalization to ensure cross-user matching works correctly
/// by stripping out variable parts like account numbers, confirmation numbers, etc.
/// </summary>
public class MerchantNormalizationTests
{
    // Use reflection to access the private NormalizeMerchantForMapping method
    private static string NormalizeMerchant(string merchant)
    {
        var serviceType = typeof(BudgetTracker.Common.Services.Categories.OptimizedCategoryAssignmentService);
        var method = serviceType.GetMethod("NormalizeMerchantForMapping",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new Exception("NormalizeMerchantForMapping method not found");

        return (string)method.Invoke(null, new object[] { merchant })!;
    }

    [Theory]
    [InlineData("Online Banking transfer to BRK 2826 Confirmation# XXXXX86417", "ONLINE BANKING TRANSFER")]
    [InlineData("Online Banking transfer to CHK 7794 Confirmation# XXXXX70901", "ONLINE BANKING TRANSFER")]
    [InlineData("Online Banking transfer from BRK 2826 Confirmation# XXXXX86219", "ONLINE BANKING TRANSFER")]
    [InlineData("Online Banking transfer to SAV 6119 Confirmation# XXXXX26472", "ONLINE BANKING TRANSFER")]
    public void NormalizeMerchant_RemovesAccountNumbers(string input, string expected)
    {
        // Act
        var result = NormalizeMerchant(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Zelle payment from DEEPIKA KUMARAGURU for burlington; Conf# 99c1pylzx", "ZELLE PAYMENT")]
    [InlineData("Zelle payment from JOHN DOE for rent; Confirmation# 123abc", "ZELLE PAYMENT")]
    [InlineData("Venmo to JANE SMITH for dinner", "VENMO")]
    public void NormalizeMerchant_RemovesPersonalNames(string input, string expected)
    {
        // Act
        var result = NormalizeMerchant(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("PURCHASE AT STARBUCKS STORE #1234 CONF# ABC123", "PURCHASE AT STARBUCKS STORE #1234")]
    [InlineData("WALMART SUPERCENTER Confirmation# XXXXX12345", "WALMART SUPERCENTER")]
    [InlineData("AMAZON.COM*AB12CD34 Conf #999", "AMAZON.COM*AB12CD34")]
    public void NormalizeMerchant_RemovesConfirmationNumbers(string input, string expected)
    {
        // Act
        var result = NormalizeMerchant(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("AMAZON.COM INC", "AMAZON.COM")]
    [InlineData("TARGET CORPORATION", "TARGET")]
    [InlineData("COSTCO WHOLESALE LLC", "COSTCO WHOLESALE")]
    [InlineData("BEST BUY CO", "BEST BUY")]
    public void NormalizeMerchant_RemovesCommonSuffixes(string input, string expected)
    {
        // Act
        var result = NormalizeMerchant(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Payment to friend for dinner", "PAYMENT")]
    [InlineData("Transfer for rent payment", "TRANSFER")]
    [InlineData("VENMO FOR LUNCH", "VENMO")]
    public void NormalizeMerchant_RemovesDetailsAfterFor(string input, string expected)
    {
        // Act
        var result = NormalizeMerchant(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  STARBUCKS  STORE  ", "STARBUCKS STORE")]
    [InlineData("WHOLE   FOODS   MARKET", "WHOLE FOODS MARKET")]
    [InlineData("target   store", "TARGET STORE")]
    public void NormalizeMerchant_NormalizesWhitespace(string input, string expected)
    {
        // Act
        var result = NormalizeMerchant(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeMerchant_SameTypeOfTransactions_ProduceSameNormalization()
    {
        // Arrange - different instances of the same transaction type
        var transaction1 = "Online Banking transfer to BRK 2826 Confirmation# XXXXX86417";
        var transaction2 = "Online Banking transfer to CHK 7794 Confirmation# XXXXX70901";
        var transaction3 = "Online Banking transfer from SAV 6119 Confirmation# XXXXX12345";

        // Act
        var result1 = NormalizeMerchant(transaction1);
        var result2 = NormalizeMerchant(transaction2);
        var result3 = NormalizeMerchant(transaction3);

        // Assert - all should normalize to the same value
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
        Assert.Equal("ONLINE BANKING TRANSFER", result1);
    }

    [Fact]
    public void NormalizeMerchant_ZellePayments_ProduceSameNormalization()
    {
        // Arrange - different Zelle payments with different names and confirmation numbers
        var transaction1 = "Zelle payment from DEEPIKA KUMARAGURU for burlington; Conf# 99c1pylzx";
        var transaction2 = "Zelle payment from JOHN DOE for groceries; Confirmation# abc123";
        var transaction3 = "Zelle payment from JANE SMITH for utilities; Conf# xyz789";

        // Act
        var result1 = NormalizeMerchant(transaction1);
        var result2 = NormalizeMerchant(transaction2);
        var result3 = NormalizeMerchant(transaction3);

        // Assert - all should normalize to "ZELLE PAYMENT"
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
        Assert.Equal("ZELLE PAYMENT", result1);
    }
}
