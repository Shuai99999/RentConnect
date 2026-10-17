using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.Entities;

public sealed class SharedBillTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesDraftBill()
    {
        var bill = CreateBill();

        Assert.Equal(SharedBillStatus.Draft, bill.Status);
        Assert.Equal(BillSplitMethod.Equal, bill.SplitMethod);
        Assert.Empty(bill.Shares);
        Assert.Equal("October electricity", bill.Title);
        Assert.Equal(120m, bill.Total.Amount);
        Assert.Equal("CAD", bill.Total.Currency);
    }

    [Fact]
    public void AllocateEqually_CreatesSharesWhoseTotalMatchesBill()
    {
        var bill = CreateBill(total: 100m);
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var thirdUserId = Guid.NewGuid();

        bill.AllocateEqually(
            [
                (Guid.NewGuid(), firstUserId),
                (Guid.NewGuid(), secondUserId),
                (Guid.NewGuid(), thirdUserId)
            ],
            decimalPlaces: 2,
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(3, bill.Shares.Count);
        Assert.Equal(
            bill.Total.Amount,
            bill.Shares.Sum(share => share.Amount.Amount));
        Assert.All(
            bill.Shares,
            share => Assert.Equal("CAD", share.Amount.Currency));
        Assert.Equal(
            CreatedAtUtc.AddMinutes(1),
            bill.UpdatedAtUtc);
    }

    [Fact]
    public void AllocateEqually_ASecondTime_ReplacesDraftShares()
    {
        var bill = CreateBill();

        bill.AllocateEqually(
            [
                (Guid.NewGuid(), Guid.NewGuid()),
                (Guid.NewGuid(), Guid.NewGuid())
            ],
            decimalPlaces: 2,
            CreatedAtUtc.AddMinutes(1));

        var replacementUserId = Guid.NewGuid();

        bill.AllocateEqually(
            [(Guid.NewGuid(), replacementUserId)],
            decimalPlaces: 2,
            CreatedAtUtc.AddMinutes(2));

        var share = Assert.Single(bill.Shares);
        Assert.Equal(replacementUserId, share.UserProfileId);
        Assert.Equal(120m, share.Amount.Amount);
    }

    [Fact]
    public void Publish_WithValidShares_FreezesBill()
    {
        var bill = CreateBill();
        AllocateToTwoUsers(bill);

        bill.Publish(CreatedAtUtc.AddMinutes(2));

        Assert.Equal(SharedBillStatus.Published, bill.Status);

        Assert.Throws<InvalidOperationException>(
            () => bill.AllocateEqually(
                [(Guid.NewGuid(), Guid.NewGuid())],
                decimalPlaces: 2,
                CreatedAtUtc.AddMinutes(3)));
    }

    [Fact]
    public void Publish_WithoutShares_ThrowsException()
    {
        var bill = CreateBill();

        Assert.Throws<InvalidOperationException>(
            () => bill.Publish(CreatedAtUtc.AddMinutes(1)));

        Assert.Equal(SharedBillStatus.Draft, bill.Status);
    }

    [Fact]
    public void Publish_WithInvalidTimestamp_DoesNotChangeStatus()
    {
        var bill = CreateBill();
        AllocateToTwoUsers(bill);

        Assert.Throws<ArgumentException>(
            () => bill.Publish(CreatedAtUtc));

        Assert.Equal(SharedBillStatus.Draft, bill.Status);
    }

    [Fact]
    public void Void_FromPublished_ChangesStatusAndKeepsShares()
    {
        var bill = CreateBill();
        AllocateToTwoUsers(bill);
        bill.Publish(CreatedAtUtc.AddMinutes(2));

        bill.Void(CreatedAtUtc.AddMinutes(3));

        Assert.Equal(SharedBillStatus.Void, bill.Status);
        Assert.Equal(2, bill.Shares.Count);
    }

    [Fact]
    public void Constructor_WithInvalidBillingPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentException>(
            () => new SharedBill(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Electricity",
                SharedBillCategory.Electricity,
                new Money(100m, "CAD"),
                new DateOnly(2026, 10, 31),
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 11, 15),
                CreatedAtUtc));
    }

    [Fact]
    public void ConfirmSharePayment_AfterDeclaration_UpdatesSettlementStatus()
    {
        var (bill, firstUserId, secondUserId) =
            CreatePublishedBill();

        bill.DeclareSharePayment(
            firstUserId,
            CreatedAtUtc.AddMinutes(3));
        bill.ConfirmSharePayment(
            firstUserId,
            CreatedAtUtc.AddMinutes(4));

        Assert.Equal(
            SharedBillStatus.PartiallySettled,
            bill.Status);

        bill.DeclareSharePayment(
            secondUserId,
            CreatedAtUtc.AddMinutes(5));
        bill.ConfirmSharePayment(
            secondUserId,
            CreatedAtUtc.AddMinutes(6));

        Assert.Equal(SharedBillStatus.Settled, bill.Status);
        Assert.All(bill.Shares, share => Assert.True(share.IsSettled));
    }

    [Fact]
    public void ConfirmSharePayment_WithoutDeclaration_ThrowsException()
    {
        var (bill, firstUserId, _) = CreatePublishedBill();

        Assert.Throws<InvalidOperationException>(
            () => bill.ConfirmSharePayment(
                firstUserId,
                CreatedAtUtc.AddMinutes(3)));

        Assert.Equal(SharedBillStatus.Published, bill.Status);
    }

    [Fact]
    public void RaiseDispute_PreservesReasonAndChangesStatus()
    {
        var (bill, firstUserId, _) = CreatePublishedBill();
        var disputeId = Guid.NewGuid();

        bill.RaiseDispute(
            disputeId,
            firstUserId,
            " The amount does not match the invoice. ",
            CreatedAtUtc.AddMinutes(3));

        Assert.Equal(SharedBillStatus.Disputed, bill.Status);

        var dispute = Assert.Single(bill.Disputes);
        Assert.Equal(disputeId, dispute.Id);
        Assert.Equal(
            "The amount does not match the invoice.",
            dispute.Reason);
        Assert.False(dispute.IsResolved);
    }

    [Fact]
    public void ConfirmingEveryShare_DuringDispute_KeepsBillDisputed()
    {
        var (bill, firstUserId, secondUserId) =
            CreatePublishedBill();
        var disputeId = Guid.NewGuid();

        bill.RaiseDispute(
            disputeId,
            firstUserId,
            "The amount needs review.",
            CreatedAtUtc.AddMinutes(3));

        bill.DeclareSharePayment(
            firstUserId,
            CreatedAtUtc.AddMinutes(4));
        bill.ConfirmSharePayment(
            firstUserId,
            CreatedAtUtc.AddMinutes(5));
        bill.DeclareSharePayment(
            secondUserId,
            CreatedAtUtc.AddMinutes(6));
        bill.ConfirmSharePayment(
            secondUserId,
            CreatedAtUtc.AddMinutes(7));

        Assert.Equal(SharedBillStatus.Disputed, bill.Status);

        bill.ResolveDispute(
            disputeId,
            Guid.NewGuid(),
            "The invoice was verified.",
            CreatedAtUtc.AddMinutes(8));

        Assert.Equal(SharedBillStatus.Settled, bill.Status);

        var dispute = Assert.Single(bill.Disputes);
        Assert.True(dispute.IsResolved);
        Assert.Equal("The invoice was verified.", dispute.Resolution);
    }

    [Fact]
    public void ResolveDispute_WithInvalidResolution_DoesNotPartiallyResolve()
    {
        var (bill, firstUserId, _) = CreatePublishedBill();
        var disputeId = Guid.NewGuid();

        bill.RaiseDispute(
            disputeId,
            firstUserId,
            "The amount needs review.",
            CreatedAtUtc.AddMinutes(3));

        Assert.Throws<ArgumentException>(
            () => bill.ResolveDispute(
                disputeId,
                Guid.NewGuid(),
                " ",
                CreatedAtUtc.AddMinutes(4)));

        Assert.Equal(SharedBillStatus.Disputed, bill.Status);
        var dispute = Assert.Single(bill.Disputes);
        Assert.False(dispute.IsResolved);
        Assert.Null(dispute.ResolvedByUserProfileId);
    }

    private static SharedBill CreateBill(decimal total = 120m)
    {
        return new SharedBill(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            " October electricity ",
            SharedBillCategory.Electricity,
            new Money(total, "CAD"),
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 11, 15),
            CreatedAtUtc);
    }

    private static void AllocateToTwoUsers(SharedBill bill)
    {
        bill.AllocateEqually(
            [
                (Guid.NewGuid(), Guid.NewGuid()),
                (Guid.NewGuid(), Guid.NewGuid())
            ],
            decimalPlaces: 2,
            CreatedAtUtc.AddMinutes(1));
    }

    private static (
        SharedBill Bill,
        Guid FirstUserId,
        Guid SecondUserId) CreatePublishedBill()
    {
        var bill = CreateBill();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        bill.AllocateEqually(
            [
                (Guid.NewGuid(), firstUserId),
                (Guid.NewGuid(), secondUserId)
            ],
            decimalPlaces: 2,
            CreatedAtUtc.AddMinutes(1));

        bill.Publish(CreatedAtUtc.AddMinutes(2));

        return (bill, firstUserId, secondUserId);
    }
}
