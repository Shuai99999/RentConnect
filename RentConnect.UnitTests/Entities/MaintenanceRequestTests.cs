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

        request.Acknowledge(acknowledgedAt);

        request.Schedule(
            scheduledAt,
            schedulingChangedAt);

        request.StartWork(workStartedAt);

        request.Resolve(
            "The leaking pipe was replaced.",
            resolvedAt);

        request.Close(closedAt);

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
    }

    [Fact]
    public void Schedule_InThePast_ThrowsException()
    {
        var request = CreateRequest();

        var acknowledgedAt =
            CreatedAtUtc.AddHours(1);

        request.Acknowledge(acknowledgedAt);

        var schedulingChangedAt =
            CreatedAtUtc.AddHours(2);

        var scheduledFor =
            CreatedAtUtc.AddMinutes(30);

        var action = () =>
            request.Schedule(
                scheduledFor,
                schedulingChangedAt);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Resolve_WithoutResolutionNotes_ThrowsException()
    {
        var request = CreateRequest();

        request.Acknowledge(
            CreatedAtUtc.AddHours(1));

        request.StartWork(
            CreatedAtUtc.AddHours(2));

        var action = () =>
            request.Resolve(
                "   ",
                CreatedAtUtc.AddHours(3));

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Cancel_AfterAcknowledgement_ThrowsException()
    {
        var request = CreateRequest();

        request.Acknowledge(
            CreatedAtUtc.AddHours(1));

        var action = () =>
            request.Cancel(
                CreatedAtUtc.AddHours(2));

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void StatusChange_WithEarlierTimestamp_ThrowsException()
    {
        var request = CreateRequest();

        request.Acknowledge(
            CreatedAtUtc.AddHours(2));

        var action = () =>
            request.StartWork(
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
            CreatedAtUtc.AddHours(1));

        request.StartWork(
            CreatedAtUtc.AddHours(2));

        var action = () =>
            request.Resolve(
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
