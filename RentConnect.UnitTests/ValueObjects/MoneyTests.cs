using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_WithValidValues_NormalizesCurrency()
    {
        var money = new Money(125.50m, " cad ");

        Assert.Equal(125.50m, money.Amount);
        Assert.Equal("CAD", money.Currency);
    }

    [Fact]
    public void Constructor_WithNegativeAmount_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Money(-0.01m, "CAD"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("CA")]
    [InlineData("CADS")]
    [InlineData("12A")]
    public void Constructor_WithInvalidCurrency_ThrowsException(
        string currency)
    {
        Assert.Throws<ArgumentException>(
            () => new Money(10m, currency));
    }
}
