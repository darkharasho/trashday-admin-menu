using AdminMenu;
using Xunit;

public class AmountsTests
{
    [Theory]
    [InlineData("100", 100f)]
    [InlineData(" 250 ", 250f)]
    [InlineData("$1,000", 1000f)]
    [InlineData("12.5", 12.5f)]
    [InlineData("1000000", 1000000f)]
    public void ParsesPositiveAmounts(string text, float expected)
    {
        Assert.True(Amounts.TryParseMoney(text, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1000001")]
    [InlineData("1e9")]
    public void RefusesEmptyJunkNonPositiveAndHugeAmounts(string text)
    {
        Assert.False(Amounts.TryParseMoney(text, out _));
    }
}
