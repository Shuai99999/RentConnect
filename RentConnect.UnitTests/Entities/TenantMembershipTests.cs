using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public class TenantMembershipTests
{
    [Fact]
    public void AddMember_WithValidDate_AddsMember()
    {
        var tenancy = CreateTenancy();
        var userProfileId = Guid.NewGuid();

        var member = tenancy.AddMember(
            Guid.NewGuid(),
            userProfileId,
            new DateOnly(2026, 2, 1));

        Assert.Single(tenancy.Members);
        Assert.Equal(tenancy.Id, member.TenancyId);
        Assert.Equal(userProfileId, member.UserProfileId);
        Assert.Equal(new DateOnly(2026, 2, 1), member.JoinedOn);
        Assert.Null(member.LeftOn);
    }

    [Fact]
    public void AddMember_BeforeTenancyStart_ThrowsException()
    {
        var tenancy = CreateTenancy();

        var action = () => tenancy.AddMember(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2025, 12, 31));

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void AddMember_WhenMembershipOverlaps_ThrowsException()
    {
        var tenancy = CreateTenancy();
        var userProfileId = Guid.NewGuid();

        tenancy.AddMember(
            Guid.NewGuid(),
            userProfileId,
            new DateOnly(2026, 1, 1));

        var action = () => tenancy.AddMember(
            Guid.NewGuid(),
            userProfileId,
            new DateOnly(2026, 6, 1));

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void MarkMemberLeft_EndsMembershipInclusively()
    {
        var tenancy = CreateTenancy();
        var userProfileId = Guid.NewGuid();

        var member = tenancy.AddMember(
            Guid.NewGuid(),
            userProfileId,
            new DateOnly(2026, 1, 1));

        tenancy.MarkMemberLeft(
            userProfileId,
            new DateOnly(2026, 6, 30));

        Assert.Equal(new DateOnly(2026, 6, 30), member.LeftOn);
        Assert.True(member.IsActiveOn(new DateOnly(2026, 6, 30)));
        Assert.False(member.IsActiveOn(new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void IsMemberActiveOn_WhenTenancyIsCancelled_ReturnsFalse()
    {
        var tenancy = CreateTenancy();
        var userProfileId = Guid.NewGuid();

        tenancy.AddMember(
            Guid.NewGuid(),
            userProfileId,
            new DateOnly(2026, 1, 1));

        tenancy.Cancel();

        Assert.False(
            tenancy.IsMemberActiveOn(
                userProfileId,
                new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void AddMember_AfterPreviousMembershipEnds_AllowsRejoining()
    {
        var tenancy = CreateTenancy();
        var userProfileId = Guid.NewGuid();

        tenancy.AddMember(
            Guid.NewGuid(),
            userProfileId,
            new DateOnly(2026, 1, 1));

        tenancy.MarkMemberLeft(
            userProfileId,
            new DateOnly(2026, 3, 31));

        tenancy.AddMember(
            Guid.NewGuid(),
            userProfileId,
            new DateOnly(2026, 4, 1));

        Assert.Equal(2, tenancy.Members.Count);
    }

    [Fact]
    public void ChangeEndDate_BeforeMemberJoinDate_ThrowsException()
    {
        var tenancy = CreateTenancy();

        tenancy.AddMember(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1));

        var action = () =>
            tenancy.ChangeEndDate(new DateOnly(2026, 5, 31));

        Assert.Throws<InvalidOperationException>(action);
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