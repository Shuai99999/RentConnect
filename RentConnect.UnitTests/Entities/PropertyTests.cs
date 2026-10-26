using RentConnect.Core.Entities;
using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.Entities;

public class PropertyTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesProperty()
    {
        var propertyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var address = CreateAddress("100 Main Street");
        var createdAtUtc = new DateTimeOffset(
            2026, 10, 22, 18, 30, 0, TimeSpan.Zero);

        var property = new Property(
            propertyId,
            "  Downtown Apartment  ",
            address,
            ownerId,
            createdAtUtc);

        Assert.Equal(propertyId, property.Id);
        Assert.Equal("Downtown Apartment", property.Name);
        Assert.Equal(address, property.Address);
        Assert.Null(property.Location);
        Assert.Equal(createdAtUtc, property.CreatedAtUtc);

        var owner = Assert.Single(property.Owners);
        Assert.Equal(propertyId, owner.PropertyId);
        Assert.Equal(ownerId, owner.UserProfileId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutName_ThrowsException(string name)
    {
        var action = () => new Property(
            Guid.NewGuid(),
            name,
            CreateAddress("100 Main Street"),
            Guid.NewGuid());

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void SetLocation_WithValidCoordinate_SetsLocation()
    {
        var property = CreateProperty();
        var location = new GeoCoordinate(43.6532, -79.3832);

        property.SetLocation(location);

        Assert.Equal(location, property.Location);
    }

    [Fact]
    public void ChangeAddress_ClearsExistingLocation()
    {
        var property = CreateProperty();
        property.SetLocation(
            new GeoCoordinate(43.6532, -79.3832));

        var newAddress = CreateAddress("200 King Street");

        property.ChangeAddress(newAddress);

        Assert.Equal(newAddress, property.Address);
        Assert.Null(property.Location);
    }

    [Fact]
    public void AddOwner_WithNewOwner_AddsOwner()
    {
        var property = CreateProperty();
        var newOwnerId = Guid.NewGuid();

        property.AddOwner(newOwnerId);

        Assert.Equal(2, property.Owners.Count);
        Assert.Contains(
            property.Owners,
            owner => owner.UserProfileId == newOwnerId);
    }

    [Fact]
    public void AddOwner_WithExistingOwner_ThrowsException()
    {
        var ownerId = Guid.NewGuid();

        var property = new Property(
            Guid.NewGuid(),
            "Downtown Apartment",
            CreateAddress("100 Main Street"),
            ownerId);

        var action = () => property.AddOwner(ownerId);

        Assert.Throws<InvalidOperationException>(action);
    }

    private static Property CreateProperty()
    {
        return new Property(
            Guid.NewGuid(),
            "Downtown Apartment",
            CreateAddress("100 Main Street"),
            Guid.NewGuid());
    }

    private static Address CreateAddress(string line1)
    {
        return new Address(
            line1,
            line2: null,
            city: "Toronto",
            region: "Ontario",
            postalCode: "A1A 1A1",
            countryCode: "CA");
    }
}
