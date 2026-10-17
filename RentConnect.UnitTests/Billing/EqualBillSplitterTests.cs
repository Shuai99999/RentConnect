using RentConnect.Core.Billing;
using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.Billing;

public sealed class EqualBillSplitterTests
{
    [Fact]
    public void Split_WhenTotalDividesEvenly_AssignsEqualAmounts()
    {
        var participantIds = new[]
        {
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000002")
        };

        var allocations = EqualBillSplitter.Split(
            new Money(80m, "CAD"),
            participantIds);

        Assert.All(
            allocations,
            allocation => Assert.Equal(40m, allocation.Amount.Amount));
    }

    [Fact]
    public void Split_WithRemainder_DistributesSmallestUnitsDeterministically()
    {
        var firstId =
            Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondId =
            Guid.Parse("10000000-0000-0000-0000-000000000002");
        var thirdId =
            Guid.Parse("10000000-0000-0000-0000-000000000003");

        var allocations = EqualBillSplitter.Split(
            new Money(100m, "CAD"),
            [thirdId, firstId, secondId]);

        Assert.Collection(
            allocations,
            allocation =>
            {
                Assert.Equal(firstId, allocation.UserProfileId);
                Assert.Equal(33.34m, allocation.Amount.Amount);
            },
            allocation =>
            {
                Assert.Equal(secondId, allocation.UserProfileId);
                Assert.Equal(33.33m, allocation.Amount.Amount);
            },
            allocation =>
            {
                Assert.Equal(thirdId, allocation.UserProfileId);
                Assert.Equal(33.33m, allocation.Amount.Amount);
            });

        Assert.Equal(
            100m,
            allocations.Sum(allocation => allocation.Amount.Amount));
    }

    [Fact]
    public void Split_WithZeroDecimalCurrency_PreservesTotal()
    {
        var allocations = EqualBillSplitter.Split(
            new Money(10m, "JPY"),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            decimalPlaces: 0);

        Assert.Equal(
            10m,
            allocations.Sum(allocation => allocation.Amount.Amount));
        Assert.All(
            allocations,
            allocation =>
                Assert.Equal(
                    decimal.Truncate(allocation.Amount.Amount),
                    allocation.Amount.Amount));
    }

    [Fact]
    public void Split_WithDuplicateParticipant_ThrowsException()
    {
        var participantId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => EqualBillSplitter.Split(
                new Money(20m, "CAD"),
                [participantId, participantId]));
    }

    [Fact]
    public void Split_WithUnsupportedPrecision_ThrowsException()
    {
        Assert.Throws<ArgumentException>(
            () => EqualBillSplitter.Split(
                new Money(10.001m, "CAD"),
                [Guid.NewGuid()],
                decimalPlaces: 2));
    }
}
