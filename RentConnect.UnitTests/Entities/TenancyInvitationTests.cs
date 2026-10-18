using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public sealed class TenancyInvitationTests
{
    [Fact]
    public void Invitation_CanBeAcceptedOnceBeforeExpiry()
    {
        var created = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var invitation = CreateInvitation(created);

        Assert.True(invitation.CanBeAcceptedAt(created.AddDays(1)));

        invitation.Accept(created.AddDays(1));

        Assert.False(invitation.CanBeAcceptedAt(created.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => invitation.Accept(created.AddDays(2)));
    }

    [Fact]
    public void Invitation_RevokedOrExpired_CannotBeAccepted()
    {
        var created = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var revoked = CreateInvitation(created);
        revoked.Revoke(created.AddHours(1));

        Assert.False(revoked.CanBeAcceptedAt(created.AddHours(2)));
        Assert.False(CreateInvitation(created).CanBeAcceptedAt(created.AddDays(7)));
    }

    [Theory]
    [InlineData(" Tenant@Example.com ", "TENANT@EXAMPLE.COM")]
    [InlineData("person+home@example.com", "PERSON+HOME@EXAMPLE.COM")]
    public void NormalizeEmail_ProducesStableLookupValue(string email, string expected)
    {
        Assert.Equal(expected, TenancyInvitation.NormalizeEmail(email));
    }

    private static TenancyInvitation CreateInvitation(DateTimeOffset created) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "tenant@example.com",
            new string('A', 64),
            Guid.NewGuid(),
            new DateOnly(2026, 11, 1),
            created,
            created.AddDays(7));
}
