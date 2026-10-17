using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.Policies;
using RentConnect.Core.ValueObjects;

namespace RentConnect.UnitTests.Policies;

public sealed class SharedBillAccessPolicyTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CanView_AllowsOwnerAndActiveMember()
    {
        Assert.True(SharedBillAccessPolicy.CanView(
            CreateAccess(isOwner: true)));
        Assert.True(SharedBillAccessPolicy.CanView(
            CreateAccess(isActiveMember: true)));
        Assert.False(SharedBillAccessPolicy.CanView(
            CreateAccess()));
    }

    [Fact]
    public void CanPublish_AllowsCreatorOrOwnerForDraftOnly()
    {
        var creatorId = Guid.NewGuid();
        var bill = CreateBill(creatorId, Guid.NewGuid());

        Assert.True(SharedBillAccessPolicy.CanPublish(
            bill,
            CreateAccess(currentUserId: creatorId)));
        Assert.True(SharedBillAccessPolicy.CanPublish(
            bill,
            CreateAccess(isOwner: true)));

        Publish(bill);

        Assert.False(SharedBillAccessPolicy.CanPublish(
            bill,
            CreateAccess(currentUserId: creatorId)));
    }

    [Fact]
    public void CanDeclarePayment_RequiresActiveMemberWithShare()
    {
        var bill = CreateBill(Guid.NewGuid(), Guid.NewGuid());
        Publish(bill);

        Assert.True(SharedBillAccessPolicy.CanDeclarePayment(
            bill,
            CreateAccess(
                isActiveMember: true,
                hasShare: true)));
        Assert.False(SharedBillAccessPolicy.CanDeclarePayment(
            bill,
            CreateAccess(
                isActiveMember: true,
                hasShare: false)));
        Assert.False(SharedBillAccessPolicy.CanDeclarePayment(
            bill,
            CreateAccess(
                isActiveMember: false,
                hasShare: true)));
    }

    [Fact]
    public void CanConfirmPayment_RequiresPayingUser()
    {
        var paidByUserId = Guid.NewGuid();
        var bill = CreateBill(Guid.NewGuid(), paidByUserId);
        Publish(bill);

        Assert.True(SharedBillAccessPolicy.CanConfirmPayment(
            bill,
            CreateAccess(
                currentUserId: paidByUserId,
                isActiveMember: true)));
        Assert.False(SharedBillAccessPolicy.CanConfirmPayment(
            bill,
            CreateAccess(
                currentUserId: Guid.NewGuid(),
                isActiveMember: true)));
    }

    [Fact]
    public void CanRaiseDispute_RequiresActiveMemberWithShare()
    {
        var bill = CreateBill(Guid.NewGuid(), Guid.NewGuid());
        Publish(bill);

        Assert.True(SharedBillAccessPolicy.CanRaiseDispute(
            bill,
            CreateAccess(
                isActiveMember: true,
                hasShare: true)));
        Assert.False(SharedBillAccessPolicy.CanRaiseDispute(
            bill,
            CreateAccess(isOwner: true)));
    }

    [Fact]
    public void CanResolveDispute_AllowsOwnerCreatorOrPayingUser()
    {
        var creatorId = Guid.NewGuid();
        var paidByUserId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var bill = CreateBill(creatorId, paidByUserId, memberId);
        Publish(bill);
        bill.RaiseDispute(
            Guid.NewGuid(),
            memberId,
            "The amount needs review.",
            CreatedAtUtc.AddMinutes(2));

        Assert.True(SharedBillAccessPolicy.CanResolveDispute(
            bill,
            CreateAccess(isOwner: true)));
        Assert.True(SharedBillAccessPolicy.CanResolveDispute(
            bill,
            CreateAccess(currentUserId: creatorId)));
        Assert.True(SharedBillAccessPolicy.CanResolveDispute(
            bill,
            CreateAccess(currentUserId: paidByUserId)));
        Assert.False(SharedBillAccessPolicy.CanResolveDispute(
            bill,
            CreateAccess(currentUserId: memberId)));
    }

    private static SharedBillAccessContext CreateAccess(
        Guid? currentUserId = null,
        bool isOwner = false,
        bool isActiveMember = false,
        bool hasShare = false)
    {
        return new SharedBillAccessContext(
            currentUserId ?? Guid.NewGuid(),
            isOwner,
            isActiveMember,
            hasShare);
    }

    private static SharedBill CreateBill(
        Guid creatorId,
        Guid paidByUserId,
        Guid? participantId = null)
    {
        var bill = new SharedBill(
            Guid.NewGuid(),
            Guid.NewGuid(),
            creatorId,
            paidByUserId,
            "Electricity",
            SharedBillCategory.Electricity,
            new Money(100m, "CAD"),
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 11, 15),
            CreatedAtUtc);

        bill.AllocateEqually(
            [(Guid.NewGuid(), participantId ?? Guid.NewGuid())],
            decimalPlaces: 2,
            CreatedAtUtc.AddMinutes(1));

        return bill;
    }

    private static void Publish(SharedBill bill)
    {
        bill.Publish(CreatedAtUtc.AddMinutes(1));
    }
}
