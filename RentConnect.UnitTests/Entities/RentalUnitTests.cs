using RentConnect.Core.Entities;
using RentConnect.Core.Enums;

namespace RentConnect.UnitTests.Entities;

public class RentalUnitTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesActiveUnit()
    {
        var id = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var unit = new RentalUnit(
            id,
            propertyId,
            "  Unit 101  ",
            RentalUnitType.Apartment);

        Assert.Equal(id, unit.Id);
        Assert.Equal(propertyId, unit.PropertyId);
        Assert.Equal("Unit 101", unit.Name);
        Assert.Equal(RentalUnitType.Apartment, unit.Type);
        Assert.True(unit.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutName_ThrowsException(string name)
    {
        var action = () => new RentalUnit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            name,
            RentalUnitType.Apartment);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_WithoutPropertyId_ThrowsException()
    {
        var action = () => new RentalUnit(
            Guid.NewGuid(),
            Guid.Empty,
            "Unit 101",
            RentalUnitType.Apartment);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_WithInvalidType_ThrowsException()
    {
        var invalidType = (RentalUnitType)999;

        var action = () => new RentalUnit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unit 101",
            invalidType);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Rename_WithValidName_ChangesName()
    {
        var unit = CreateUnit();

        unit.Rename("  Unit 102  ");

        Assert.Equal("Unit 102", unit.Name);
    }

    [Fact]
    public void ChangeType_WithValidType_ChangesType()
    {
        var unit = CreateUnit();

        unit.ChangeType(RentalUnitType.Room);

        Assert.Equal(RentalUnitType.Room, unit.Type);
    }

    [Fact]
    public void ChangeType_WithInvalidType_ThrowsException()
    {
        var unit = CreateUnit();

        var action = () => unit.ChangeType((RentalUnitType)999);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Deactivate_WhenActive_MakesUnitInactive()
    {
        var unit = CreateUnit();

        unit.Deactivate();

        Assert.False(unit.IsActive);
    }

    [Fact]
    public void Activate_WhenInactive_MakesUnitActive()
    {
        var unit = CreateUnit();
        unit.Deactivate();

        unit.Activate();

        Assert.True(unit.IsActive);
    }

    [Fact]
    public void Constructor_WithNameLongerThanMaximum_ThrowsException()
    {
        var longName = new string(
            'A',
            RentalUnit.MaxNameLength + 1);

        var action = () => new RentalUnit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            longName,
            RentalUnitType.Apartment);

        Assert.Throws<ArgumentException>(action);
    }

    private static RentalUnit CreateUnit()
    {
        return new RentalUnit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unit 101",
            RentalUnitType.Apartment);
    }
}
