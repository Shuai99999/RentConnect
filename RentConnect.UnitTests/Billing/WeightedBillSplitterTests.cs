using RentConnect.Core.Billing;
using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.Billing;

public sealed class WeightedBillSplitterTests
{
    [Fact]
    public void Split_DistributesRoundingRemainderAndPreservesTotal()
    {
        var first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var third = Guid.Parse("10000000-0000-0000-0000-000000000003");

        var result = WeightedBillSplitter.Split(
            new Money(10m, "CAD"),
            [(first, 50m), (second, 30m), (third, 20m)]);

        Assert.Collection(
            result,
            item => Assert.Equal(5m, item.Amount.Amount),
            item => Assert.Equal(3m, item.Amount.Amount),
            item => Assert.Equal(2m, item.Amount.Amount));
        Assert.Equal(10m, result.Sum(item => item.Amount.Amount));
    }

    [Fact]
    public void Split_WhenFractionsTie_UsesStableParticipantOrder()
    {
        var first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var third = Guid.Parse("10000000-0000-0000-0000-000000000003");

        var result = WeightedBillSplitter.Split(
            new Money(1m, "CAD"),
            [(third, 1m), (second, 1m), (first, 1m)]);

        Assert.Equal(0.34m, result[0].Amount.Amount);
        Assert.Equal(0.33m, result[1].Amount.Amount);
        Assert.Equal(0.33m, result[2].Amount.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Split_RejectsNonPositiveWeight(decimal weight)
    {
        Assert.Throws<ArgumentException>(() => WeightedBillSplitter.Split(
            new Money(10m, "CAD"),
            [(Guid.NewGuid(), weight)]));
    }
}
