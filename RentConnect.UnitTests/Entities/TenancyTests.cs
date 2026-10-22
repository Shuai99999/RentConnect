using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public class TenancyTests
{
    [Fact]
    public void Constructor_WithValidDates_CreatesTenancy()
    {
        var id = Guid.NewGuid();
        var rentalUnitId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 12, 31);
        var createdAtUtc = new DateTimeOffset(
            2026, 10, 22, 18, 30, 0, TimeSpan.Zero);

        var tenancy = new Tenancy(
            id,
            rentalUnitId,
            startDate,
            endDate,
            createdAtUtc);

        Assert.Equal(id, tenancy.Id);
        Assert.Equal(rentalUnitId, tenancy.RentalUnitId);
        Assert.Equal(startDate, tenancy.StartDate);
        Assert.Equal(endDate, tenancy.EndDate);
        Assert.Equal(createdAtUtc, tenancy.CreatedAtUtc);
        Assert.False(tenancy.IsCancelled);
    }

    [Fact]
    public void Constructor_WithEndDateBeforeStartDate_ThrowsException()
    {
        var action = () => new Tenancy(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startDate: new DateOnly(2026, 2, 1),
            endDate: new DateOnly(2026, 1, 31));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void IsActiveOn_ForFixedTermTenancy_UsesInclusiveBoundaries()
    {
        var tenancy = new Tenancy(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        Assert.False(
            tenancy.IsActiveOn(new DateOnly(2025, 12, 31)));

        Assert.True(
            tenancy.IsActiveOn(new DateOnly(2026, 1, 1)));

        Assert.True(
            tenancy.IsActiveOn(new DateOnly(2026, 12, 31)));

        Assert.False(
            tenancy.IsActiveOn(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void IsActiveOn_ForOpenEndedTenancy_RemainsActive()
    {
        var tenancy = new Tenancy(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startDate: new DateOnly(2026, 1, 1),
            endDate: null);

        Assert.True(
            tenancy.IsActiveOn(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void Cancel_MakesTenancyInactive()
    {
        var tenancy = CreateTenancy();

        tenancy.Cancel();

        Assert.True(tenancy.IsCancelled);
        Assert.False(
            tenancy.IsActiveOn(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void ChangeEndDate_WithValidDate_ChangesEndDate()
    {
        var tenancy = CreateTenancy();
        var newEndDate = new DateOnly(2027, 6, 30);

        tenancy.ChangeEndDate(newEndDate);

        Assert.Equal(newEndDate, tenancy.EndDate);
    }

    [Fact]
    public void ChangeEndDate_WithDateBeforeStart_ThrowsException()
    {
        var tenancy = CreateTenancy();

        var action = () =>
            tenancy.ChangeEndDate(new DateOnly(2025, 12, 31));

        Assert.Throws<ArgumentException>(action);
    }

    private static Tenancy CreateTenancy()
    {
        return new Tenancy(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));
    }
}
