using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.Policies;

namespace RentConnect.UnitTests.Policies;

public class MaintenanceAccessPolicyTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CanView_SharedRequest_AllowsActiveRoommate()
    {
        var request = CreateRequest();
        var roommate = CreateAccess(isActiveMember: true);

        Assert.True(
            MaintenanceAccessPolicy.CanView(request, roommate));
    }

    [Fact]
    public void CanView_PrivateRequest_DeniesRoommate()
    {
        var request = CreateRequest(
            MaintenanceVisibility.PrivateToReporterAndManagement);
        var roommate = CreateAccess(isActiveMember: true);

        Assert.False(
            MaintenanceAccessPolicy.CanView(request, roommate));
    }

    [Fact]
    public void CanView_PrivateRequest_AllowsReporterAndOwner()
    {
        var request = CreateRequest(
            MaintenanceVisibility.PrivateToReporterAndManagement);

        var reporter = CreateAccess(
            request.CreatedByUserProfileId,
            isActiveMember: true);
        var owner = CreateAccess(isOwner: true);

        Assert.True(
            MaintenanceAccessPolicy.CanView(request, reporter));
        Assert.True(
            MaintenanceAccessPolicy.CanView(request, owner));
    }

    [Fact]
    public void CanView_SharedRequest_DeniesUnrelatedUser()
    {
        var request = CreateRequest();
        var unrelatedUser = CreateAccess();

        Assert.False(
            MaintenanceAccessPolicy.CanView(
                request,
                unrelatedUser));
    }

    [Fact]
    public void CanView_AllowsFormerReporterReadOnlyAccess()
    {
        var request = CreateRequest();
        var formerReporter = CreateAccess(
            request.CreatedByUserProfileId);

        Assert.True(
            MaintenanceAccessPolicy.CanView(
                request,
                formerReporter));
        Assert.False(
            MaintenanceAccessPolicy.CanPostMessage(
                request,
                formerReporter));
    }

    [Fact]
    public void CanPostMessage_ClosedRequest_DeniesParticipants()
    {
        var request = CreateResolvedRequest();
        request.Close(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc.AddHours(5));

        Assert.False(
            MaintenanceAccessPolicy.CanPostMessage(
                request,
                CreateAccess(isOwner: true)));
        Assert.False(
            MaintenanceAccessPolicy.CanPostMessage(
                request,
                CreateAccess(isActiveMember: true)));
    }

    [Fact]
    public void CanEditMessage_OnlyAllowsActiveAuthor()
    {
        var request = CreateRequest();
        var authorId = Guid.NewGuid();
        var message = CreateMessage(request.Id, authorId);

        Assert.True(
            MaintenanceAccessPolicy.CanEditMessage(
                request,
                message,
                CreateAccess(authorId, isActiveMember: true)));
        Assert.False(
            MaintenanceAccessPolicy.CanEditMessage(
                request,
                message,
                CreateAccess(isOwner: true)));
    }

    [Fact]
    public void CanDeleteMessage_AllowsAuthorOrOwner()
    {
        var request = CreateRequest();
        var authorId = Guid.NewGuid();
        var message = CreateMessage(request.Id, authorId);

        Assert.True(
            MaintenanceAccessPolicy.CanDeleteMessage(
                request,
                message,
                CreateAccess(authorId, isActiveMember: true)));
        Assert.True(
            MaintenanceAccessPolicy.CanDeleteMessage(
                request,
                message,
                CreateAccess(isOwner: true)));
    }

    [Fact]
    public void CanAddAttachment_AllowsActiveParticipantAndOwner()
    {
        var request = CreateRequest();
        var activeTenant =
            CreateAccess(isActiveMember: true);
        var owner =
            CreateAccess(isOwner: true);

        Assert.True(
            MaintenanceAccessPolicy.CanAddAttachment(
                request,
                activeTenant));

        Assert.True(
            MaintenanceAccessPolicy.CanAddAttachment(
                request,
                owner));
    }

    [Fact]
    public void CanAddAttachment_DeniesUnrelatedAndFormerUsers()
    {
        var request = CreateRequest();
        var unrelatedUser = CreateAccess();
        var formerReporter =
            CreateAccess(
                request.CreatedByUserProfileId);

        Assert.False(
            MaintenanceAccessPolicy.CanAddAttachment(
                request,
                unrelatedUser));

        Assert.False(
            MaintenanceAccessPolicy.CanAddAttachment(
                request,
                formerReporter));
    }

    [Fact]
    public void CanAddAttachment_ClosedRequest_DeniesParticipants()
    {
        var request = CreateResolvedRequest();

        request.Close(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc.AddHours(5));

        Assert.False(
            MaintenanceAccessPolicy.CanAddAttachment(
                request,
                CreateAccess(isOwner: true)));

        Assert.False(
            MaintenanceAccessPolicy.CanAddAttachment(
                request,
                CreateAccess(isActiveMember: true)));
    }

    [Fact]
    public void CanDeleteAttachment_AllowsUploaderOrOwner()
    {
        var request = CreateRequest();
        var uploaderId = Guid.NewGuid();

        var attachment =
            CreateAttachment(
                request.Id,
                uploaderId);

        Assert.True(
            MaintenanceAccessPolicy.CanDeleteAttachment(
                request,
                attachment,
                CreateAccess(
                    uploaderId,
                    isActiveMember: true)));

        Assert.True(
            MaintenanceAccessPolicy.CanDeleteAttachment(
                request,
                attachment,
                CreateAccess(isOwner: true)));

        Assert.False(
            MaintenanceAccessPolicy.CanDeleteAttachment(
                request,
                attachment,
                CreateAccess(isActiveMember: true)));
    }

    [Fact]
    public void ReporterActions_RequireActiveReporter()
    {
        var request = CreateRequest();
        var activeReporter = CreateAccess(
            request.CreatedByUserProfileId,
            isActiveMember: true);
        var roommate = CreateAccess(isActiveMember: true);

        Assert.True(
            MaintenanceAccessPolicy.CanUpdateDetails(
                request,
                activeReporter));
        Assert.True(
            MaintenanceAccessPolicy.CanCancel(
                request,
                activeReporter));
        Assert.False(
            MaintenanceAccessPolicy.CanUpdateDetails(
                request,
                roommate));
        Assert.False(
            MaintenanceAccessPolicy.CanCancel(
                request,
                roommate));
    }

    [Fact]
    public void ManagementActions_RequirePropertyOwnerAndCorrectStatus()
    {
        var request = CreateRequest();
        var owner = CreateAccess(isOwner: true);
        var tenant = CreateAccess(isActiveMember: true);

        Assert.True(
            MaintenanceAccessPolicy.CanAcknowledge(request, owner));
        Assert.False(
            MaintenanceAccessPolicy.CanAcknowledge(request, tenant));

        request.Acknowledge(
            Guid.NewGuid(),
            owner.UserProfileId,
            CreatedAtUtc.AddHours(1));

        Assert.True(
            MaintenanceAccessPolicy.CanSchedule(request, owner));
        Assert.True(
            MaintenanceAccessPolicy.CanStartWork(request, owner));
        Assert.False(
            MaintenanceAccessPolicy.CanResolve(request, owner));
    }

    [Fact]
    public void TenantResolutionActions_RequireVisibleActiveTenant()
    {
        var request = CreateResolvedRequest();
        var activeTenant = CreateAccess(isActiveMember: true);
        var unrelatedUser = CreateAccess();
        var owner = CreateAccess(isOwner: true);

        Assert.True(
            MaintenanceAccessPolicy.CanClose(
                request,
                activeTenant));
        Assert.True(
            MaintenanceAccessPolicy.CanReopen(
                request,
                activeTenant));
        Assert.False(
            MaintenanceAccessPolicy.CanClose(
                request,
                unrelatedUser));
        Assert.False(
            MaintenanceAccessPolicy.CanClose(request, owner));
    }

    [Fact]
    public void TenantResolutionActions_PrivateRequestDenyRoommate()
    {
        var request = CreateResolvedRequest(
            MaintenanceVisibility.PrivateToReporterAndManagement);
        var roommate = CreateAccess(isActiveMember: true);
        var reporter = CreateAccess(
            request.CreatedByUserProfileId,
            isActiveMember: true);

        Assert.False(
            MaintenanceAccessPolicy.CanReopen(request, roommate));
        Assert.True(
            MaintenanceAccessPolicy.CanReopen(request, reporter));
    }

    private static MaintenanceRequest CreateResolvedRequest(
        MaintenanceVisibility visibility =
            MaintenanceVisibility.SharedWithTenancy)
    {
        var request = CreateRequest(visibility);

        request.Acknowledge(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc.AddHours(1));
        request.StartWork(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc.AddHours(2));
        request.Resolve(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pipe was replaced.",
            CreatedAtUtc.AddHours(4));

        return request;
    }

    private static MaintenanceRequest CreateRequest(
        MaintenanceVisibility visibility =
            MaintenanceVisibility.SharedWithTenancy)
    {
        return new MaintenanceRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Leaking kitchen tap",
            "Water is leaking continuously.",
            MaintenanceCategory.Plumbing,
            MaintenancePriority.High,
            visibility,
            CreatedAtUtc);
    }

    private static MaintenanceAttachment CreateAttachment(
        Guid requestId,
        Guid uploaderId)
    {
        return new MaintenanceAttachment(
            Guid.NewGuid(),
            requestId,
            uploaderId,
            "photo.jpg",
            "image/jpeg",
            1024,
            $"maintenance/{requestId:N}/{Guid.NewGuid():N}.jpg",
            CreatedAtUtc.AddMinutes(1));
    }

    private static MaintenanceMessage CreateMessage(
        Guid requestId,
        Guid authorId)
    {
        return new MaintenanceMessage(
            Guid.NewGuid(),
            requestId,
            authorId,
            "Message body.",
            CreatedAtUtc.AddMinutes(1));
    }

    private static MaintenanceAccessContext CreateAccess(
        Guid? userProfileId = null,
        bool isOwner = false,
        bool isActiveMember = false)
    {
        return new MaintenanceAccessContext(
            userProfileId ?? Guid.NewGuid(),
            isOwner,
            isActiveMember);
    }
}
