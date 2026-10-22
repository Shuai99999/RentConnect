using RentConnect.Core.Entities;
using RentConnect.Core.Enums;

namespace RentConnect.UnitTests.Entities;

public class MaintenanceRequestTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            2026,
            1,
            1,
            10,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesSubmittedRequest()
    {
        var id = Guid.NewGuid();
        var tenancyId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var request = new MaintenanceRequest(
            id,
            tenancyId,
            creatorId,
            "  Leaking kitchen tap  ",
            "  Water is leaking continuously.  ",
            MaintenanceCategory.Plumbing,
            MaintenancePriority.High,
            MaintenanceVisibility.SharedWithTenancy,
            CreatedAtUtc);

        Assert.Equal(id, request.Id);
        Assert.Equal(tenancyId, request.TenancyId);
        Assert.Equal(
            creatorId,
            request.CreatedByUserProfileId);

        Assert.Equal(
            "Leaking kitchen tap",
            request.Title);

        Assert.Equal(
            "Water is leaking continuously.",
            request.Description);

        Assert.Equal(
            MaintenanceStatus.Submitted,
            request.Status);

        Assert.Equal(
            CreatedAtUtc,
            request.CreatedAtUtc);

        Assert.Equal(
            CreatedAtUtc,
            request.UpdatedAtUtc);

        Assert.Null(request.ScheduledForUtc);
        Assert.Null(request.ResolutionNotes);
        Assert.Empty(request.StatusHistory);
    }

    [Fact]
    public void Constructor_WithNonUtcTimestamp_ThrowsException()
    {
        var nonUtcTime = new DateTimeOffset(
            2026,
            1,
            1,
            10,
            0,
            0,
            TimeSpan.FromHours(1));

        var action = () => new MaintenanceRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Broken heater",
            "The heater is not working.",
            MaintenanceCategory.HeatingCooling,
            MaintenancePriority.High,
            MaintenanceVisibility.SharedWithTenancy,
            nonUtcTime);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void UpdateDetails_WhileSubmitted_UpdatesRequest()
    {
        var request = CreateRequest();

        var changedAtUtc =
            CreatedAtUtc.AddMinutes(30);

        request.UpdateDetails(
            "Bathroom leak",
            "Water is leaking below the sink.",
            MaintenanceCategory.Plumbing,
            MaintenancePriority.Emergency,
            MaintenanceVisibility
                .PrivateToReporterAndManagement,
            changedAtUtc);

        Assert.Equal(
            "Bathroom leak",
            request.Title);

        Assert.Equal(
            MaintenancePriority.Emergency,
            request.Priority);

        Assert.Equal(
            MaintenanceVisibility
                .PrivateToReporterAndManagement,
            request.Visibility);

        Assert.Equal(
            changedAtUtc,
            request.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetails_AfterAcknowledgement_ThrowsException()
    {
        var request = CreateRequest();

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddMinutes(10));

        var action = () =>
            request.UpdateDetails(
                "Changed title",
                "Changed description",
                MaintenanceCategory.Other,
                MaintenancePriority.Low,
                MaintenanceVisibility.SharedWithTenancy,
                CreatedAtUtc.AddMinutes(20));

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void NormalWorkflow_ReachesClosedStatus()
    {
        var request = CreateRequest();

        var acknowledgedAt =
            CreatedAtUtc.AddHours(1);

        var scheduledAt =
            CreatedAtUtc.AddDays(1);

        var schedulingChangedAt =
            CreatedAtUtc.AddHours(2);

        var workStartedAt =
            CreatedAtUtc.AddDays(1);

        var resolvedAt =
            workStartedAt.AddHours(2);

        var closedAt =
            resolvedAt.AddHours(1);

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            acknowledgedAt);

        request.Schedule(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            scheduledAt,
            schedulingChangedAt);

        request.StartWork(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            workStartedAt);

        request.Resolve(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            "The leaking pipe was replaced.",
            resolvedAt);

        request.Close(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            closedAt);

        Assert.Equal(
            MaintenanceStatus.Closed,
            request.Status);

        Assert.Equal(
            scheduledAt,
            request.ScheduledForUtc);

        Assert.Equal(
            "The leaking pipe was replaced.",
            request.ResolutionNotes);

        Assert.Equal(
            closedAt,
            request.UpdatedAtUtc);

        Assert.Equal(
            5,
            request.StatusHistory.Count);

        Assert.Equal(
            new[]
            {
                MaintenanceStatus.Acknowledged,
                MaintenanceStatus.Scheduled,
                MaintenanceStatus.InProgress,
                MaintenanceStatus.Resolved,
                MaintenanceStatus.Closed
            },
            request.StatusHistory.Select(
                change => change.ToStatus));

        Assert.Equal(
            new[]
            {
                MaintenanceStatus.Submitted,
                MaintenanceStatus.Acknowledged,
                MaintenanceStatus.Scheduled,
                MaintenanceStatus.InProgress,
                MaintenanceStatus.Resolved
            },
            request.StatusHistory.Select(
                change => change.FromStatus));

        Assert.All(
            request.StatusHistory,
            change => Assert.Equal(
                request.CreatedByUserProfileId,
                change.ChangedByUserProfileId));

        var resolvedChange =
            request.StatusHistory.Single(
                change =>
                    change.ToStatus ==
                    MaintenanceStatus.Resolved);

        Assert.Equal(
            "The leaking pipe was replaced.",
            resolvedChange.Note);
    }

    [Fact]
    public void Schedule_InThePast_ThrowsException()
    {
        var request = CreateRequest();

        var acknowledgedAt =
            CreatedAtUtc.AddHours(1);

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            acknowledgedAt);

        var schedulingChangedAt =
            CreatedAtUtc.AddHours(2);

        var scheduledFor =
            CreatedAtUtc.AddMinutes(30);

        var action = () =>
            request.Schedule(
                Guid.NewGuid(),
                request.CreatedByUserProfileId,
                scheduledFor,
                schedulingChangedAt);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Resolve_WithoutResolutionNotes_ThrowsException()
    {
        var request = CreateRequest();

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(1));

        request.StartWork(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(2));

        var action = () =>
            request.Resolve(
                Guid.NewGuid(),
                request.CreatedByUserProfileId,
                "   ",
                CreatedAtUtc.AddHours(3));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Cancel_AfterAcknowledgement_ThrowsException()
    {
        var request = CreateRequest();

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(1));

        var action = () =>
            request.Cancel(
                Guid.NewGuid(),
                request.CreatedByUserProfileId,
                CreatedAtUtc.AddHours(2));

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void StatusChange_WithEarlierTimestamp_ThrowsException()
    {
        var request = CreateRequest();

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(2));

        var action = () =>
            request.StartWork(
                Guid.NewGuid(),
                request.CreatedByUserProfileId,
                CreatedAtUtc.AddHours(1));

        Assert.Throws<ArgumentException>(action);

        Assert.Equal(
            MaintenanceStatus.Acknowledged,
            request.Status);

        Assert.Equal(
            CreatedAtUtc.AddHours(2),
            request.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetails_WithInvalidDescription_DoesNotPartiallyUpdate()
    {
        var request = CreateRequest();

        var action = () =>
            request.UpdateDetails(
                "Changed title",
                "   ",
                MaintenanceCategory.Other,
                MaintenancePriority.Low,
                MaintenanceVisibility.SharedWithTenancy,
                CreatedAtUtc.AddHours(1));

        Assert.Throws<ArgumentException>(action);

        Assert.Equal(
            "Leaking kitchen tap",
            request.Title);

        Assert.Equal(
            "Water is leaking continuously.",
            request.Description);

        Assert.Equal(
            CreatedAtUtc,
            request.UpdatedAtUtc);
    }

    [Fact]
    public void Resolve_WithEarlierTimestamp_DoesNotPartiallyUpdate()
    {
        var request = CreateRequest();

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(1));

        request.StartWork(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(2));

        var action = () =>
            request.Resolve(
                Guid.NewGuid(),
                request.CreatedByUserProfileId,
                "Pipe was replaced.",
                CreatedAtUtc.AddHours(1));

        Assert.Throws<ArgumentException>(action);

        Assert.Equal(
            MaintenanceStatus.InProgress,
            request.Status);

        Assert.Null(request.ResolutionNotes);

        Assert.Equal(
            CreatedAtUtc.AddHours(2),
            request.UpdatedAtUtc);
    }

    [Fact]
    public void Reopen_ResolvedRequest_ReturnsToAcknowledged()
    {
        var request = CreateResolvedRequest();
        var reopenedAtUtc = CreatedAtUtc.AddHours(5);

        request.Reopen(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            "  The leak has returned.  ",
            reopenedAtUtc);

        Assert.Equal(
            MaintenanceStatus.Acknowledged,
            request.Status);
        Assert.Equal(reopenedAtUtc, request.UpdatedAtUtc);
        Assert.Null(request.ScheduledForUtc);
        Assert.Null(request.ResolutionNotes);

        var reopenChange = request.StatusHistory.Last();

        Assert.Equal(
            MaintenanceStatus.Resolved,
            reopenChange.FromStatus);
        Assert.Equal(
            MaintenanceStatus.Acknowledged,
            reopenChange.ToStatus);
        Assert.Equal(
            "The leak has returned.",
            reopenChange.Note);
    }

    [Fact]
    public void Reopen_ClosedRequest_ReturnsToAcknowledged()
    {
        var request = CreateResolvedRequest();

        request.Close(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(5));

        request.Reopen(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            "The problem happened again.",
            CreatedAtUtc.AddHours(6));

        Assert.Equal(
            MaintenanceStatus.Acknowledged,
            request.Status);
    }

    [Fact]
    public void Reopen_WithoutReason_DoesNotPartiallyUpdate()
    {
        var request = CreateResolvedRequest();
        var historyCount = request.StatusHistory.Count;

        var action = () => request.Reopen(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            "   ",
            CreatedAtUtc.AddHours(5));

        Assert.Throws<ArgumentException>(action);
        Assert.Equal(
            MaintenanceStatus.Resolved,
            request.Status);
        Assert.Equal(
            "Pipe was replaced.",
            request.ResolutionNotes);
        Assert.Equal(historyCount, request.StatusHistory.Count);
    }

    [Fact]
    public void Reopen_SubmittedRequest_ThrowsException()
    {
        var request = CreateRequest();

        var action = () => request.Reopen(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            "The problem happened again.",
            CreatedAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(action);
    }

    private static MaintenanceRequest CreateResolvedRequest()
    {
        var request = CreateRequest();

        request.Acknowledge(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(1));

        request.Schedule(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(3),
            CreatedAtUtc.AddHours(2));

        request.StartWork(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            CreatedAtUtc.AddHours(3));

        request.Resolve(
            Guid.NewGuid(),
            request.CreatedByUserProfileId,
            "Pipe was replaced.",
            CreatedAtUtc.AddHours(4));

        return request;
    }

    private static MaintenanceRequest CreateRequest()
    {
        return new MaintenanceRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Leaking kitchen tap",
            "Water is leaking continuously.",
            MaintenanceCategory.Plumbing,
            MaintenancePriority.High,
            MaintenanceVisibility.SharedWithTenancy,
            CreatedAtUtc);
    }
}
