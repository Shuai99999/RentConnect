using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Constructor_WithValidValues_NormalizesAddress()
    {
        var address = new Address(
            line1: "  100 Main Street  ",
            line2: "  Unit 2  ",
            city: "  Toronto  ",
            region: "  Ontario  ",
            postalCode: "  A1A 1A1  ",
            countryCode: "  ca  ");

        Assert.Equal("100 Main Street", address.Line1);
        Assert.Equal("Unit 2", address.Line2);
        Assert.Equal("Toronto", address.City);
        Assert.Equal("Ontario", address.Region);
        Assert.Equal("A1A 1A1", address.PostalCode);
        Assert.Equal("CA", address.CountryCode);
    }

    [Fact]
    public void TwoAddresses_WithSameValues_AreEqual()
    {
        var first = CreateAddress();
        var second = CreateAddress();

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Constructor_WithEmptyOptionalValues_StoresNull()
    {
        var address = new Address(
            line1: "100 Main Street",
            line2: " ",
            city: "Toronto",
            region: null,
            postalCode: "",
            countryCode: "CA");

        Assert.Null(address.Line2);
        Assert.Null(address.Region);
        Assert.Null(address.PostalCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("C")]
    [InlineData("CAN")]
    [InlineData("12")]
    public void Constructor_WithInvalidCountryCode_ThrowsException(
        string countryCode)
    {
        var action = () => new Address(
            line1: "100 Main Street",
            line2: null,
            city: "Toronto",
            region: "Ontario",
            postalCode: "A1A 1A1",
            countryCode: countryCode);

        Assert.Throws<ArgumentException>(action);
    }

    private static Address CreateAddress()
    {
        return new Address(
            line1: "100 Main Street",
            line2: "Unit 2",
            city: "Toronto",
            region: "Ontario",
            postalCode: "A1A 1A1",
            countryCode: "CA");
    }
}