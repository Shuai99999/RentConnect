using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Application.Maintenance;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;
using RentConnect.Infrastructure.Maintenance;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class RentalManagementPersistenceTests
{
    [Fact]
    public async Task CanPersistAndReloadRentalManagementGraph()
    {
        var connectionString = CreateConnectionString();
        var attachmentStorageRoot = Path.Combine(
            Path.GetTempPath(),
            "RentConnectIntegrationAttachments",
            Guid.NewGuid().ToString("N"));

        await using var serviceProvider =
            CreateServiceProvider(connectionString);

        var ownerProfileId = Guid.NewGuid();
        var tenantProfileId = Guid.NewGuid();
        var roommateProfileId = Guid.NewGuid();
        var unrelatedProfileId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var rentalUnitId = Guid.NewGuid();
        var tenancyId = Guid.NewGuid();
        var expiredTenancyId = Guid.NewGuid();
        var maintenanceRequestId = Guid.NewGuid();
        var privateMaintenanceRequestId = Guid.NewGuid();
        var landlordMessageId = Guid.NewGuid();
        var editedMessageId = Guid.NewGuid();
        var deletedMessageId = Guid.NewGuid();

        var maintenanceCreatedAtUtc =
            new DateTimeOffset(
                2026,
                2,
                1,
                9,
                0,
                0,
                TimeSpan.Zero);

        try
        {
            await using (var scope =
                         serviceProvider.CreateAsyncScope())
            {
                var context =
                    scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();

                await context.Database.MigrateAsync();

                var owner = new UserProfile(
                    ownerProfileId,
                    "Integration Test Owner");

                var tenant = new UserProfile(
                    tenantProfileId,
                    "Integration Test Tenant");

                var roommate = new UserProfile(
                    roommateProfileId,
                    "Integration Test Roommate");

                var property = new Property(
                    propertyId,
                    "Integration Test Property",
                    new Address(
                        "100 Test Street",
                        null,
                        "Toronto",
                        "Ontario",
                        "M1M 1M1",
                        "CA"),
                    ownerProfileId);

                property.SetLocation(
                    new GeoCoordinate(
                        43.6532,
                        -79.3832));

                var rentalUnit = new RentalUnit(
                    rentalUnitId,
                    propertyId,
                    "Unit 101",
                    RentalUnitType.Apartment);

                var tenancy = new Tenancy(
                    tenancyId,
                    rentalUnitId,
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31));

                tenancy.AddMember(
                    Guid.NewGuid(),
                    tenantProfileId,
                    new DateOnly(2026, 1, 1));

                tenancy.AddMember(
                    Guid.NewGuid(),
                    roommateProfileId,
                    new DateOnly(2026, 1, 1));

                var expiredTenancy = new Tenancy(
                    expiredTenancyId,
                    rentalUnitId,
                    new DateOnly(2025, 1, 1),
                    new DateOnly(2025, 12, 31));

                expiredTenancy.AddMember(
                    Guid.NewGuid(),
                    tenantProfileId,
                    new DateOnly(2025, 1, 1));

                var maintenanceRequest =
                    new MaintenanceRequest(
                        maintenanceRequestId,
                        tenancyId,
                        tenantProfileId,
                        "Leaking kitchen pipe",
                        "Water is leaking underneath the kitchen sink.",
                        MaintenanceCategory.Plumbing,
                        MaintenancePriority.High,
                        MaintenanceVisibility.SharedWithTenancy,
                        maintenanceCreatedAtUtc);

                maintenanceRequest.Acknowledge(
                    Guid.NewGuid(),
                    tenantProfileId,
                    maintenanceCreatedAtUtc.AddHours(1));

                maintenanceRequest.Schedule(
                    Guid.NewGuid(),
                    tenantProfileId,
                    maintenanceCreatedAtUtc.AddDays(1),
                    maintenanceCreatedAtUtc.AddHours(2));

                maintenanceRequest.StartWork(
                    Guid.NewGuid(),
                    tenantProfileId,
                    maintenanceCreatedAtUtc.AddDays(1));

                maintenanceRequest.Resolve(
                    Guid.NewGuid(),
                    tenantProfileId,
                    "The damaged pipe was replaced.",
                    maintenanceCreatedAtUtc
                        .AddDays(1)
                        .AddHours(2));

                var privateMaintenanceRequest =
                    new MaintenanceRequest(
                        privateMaintenanceRequestId,
                        tenancyId,
                        tenantProfileId,
                        "Private bedroom lock issue",
                        "The bedroom lock is not working.",
                        MaintenanceCategory.LocksAndSecurity,
                        MaintenancePriority.Normal,
                        MaintenanceVisibility
                            .PrivateToReporterAndManagement,
                        maintenanceCreatedAtUtc.AddMinutes(10));

                var landlordMessage =
                    new MaintenanceMessage(
                        landlordMessageId,
                        maintenanceRequestId,
                        ownerProfileId,
                        "A plumber has been contacted.",
                        maintenanceCreatedAtUtc.AddHours(3));

                var editedMessage =
                    new MaintenanceMessage(
                        editedMessageId,
                        maintenanceRequestId,
                        tenantProfileId,
                        "The leak is getting worse.",
                        maintenanceCreatedAtUtc.AddHours(4));

                editedMessage.Edit(
                    "The leak has slowed down.",
                    maintenanceCreatedAtUtc.AddHours(5));

                var deletedMessage =
                    new MaintenanceMessage(
                        deletedMessageId,
                        maintenanceRequestId,
                        tenantProfileId,
                        "Message to be deleted.",
                        maintenanceCreatedAtUtc.AddHours(6));

                deletedMessage.Delete(
                    ownerProfileId,
                    maintenanceCreatedAtUtc.AddHours(7));

                context.UserProfiles.AddRange(
                    owner,
                    tenant,
                    roommate);

                context.Properties.Add(property);
                context.RentalUnits.Add(rentalUnit);
                context.Tenancies.AddRange(
                    tenancy,
                    expiredTenancy);
                context.MaintenanceRequests.AddRange(
                    maintenanceRequest,
                    privateMaintenanceRequest);
                context.MaintenanceMessages.AddRange(
                    landlordMessage,
                    editedMessage,
                    deletedMessage);

                await context.SaveChangesAsync();
            }

            await using (var scope =
                         serviceProvider.CreateAsyncScope())
            {
                var context =
                    scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();

                var savedProperty =
                    await context.Properties
                        .Include(property => property.Owners)
                        .SingleAsync(
                            property =>
                                property.Id == propertyId);

                var savedRentalUnit =
                    await context.RentalUnits
                        .SingleAsync(
                            unit =>
                                unit.Id == rentalUnitId);

                var savedTenancy =
                    await context.Tenancies
                        .Include(tenancy => tenancy.Members)
                        .SingleAsync(
                            tenancy =>
                                tenancy.Id == tenancyId);

                var savedMaintenanceRequest =
                    await context.MaintenanceRequests
                        .Include(request => request.StatusHistory)
                        .SingleAsync(
                            request =>
                                request.Id ==
                                maintenanceRequestId);

                var savedMaintenanceMessages =
                    await context.MaintenanceMessages
                        .Where(
                            message =>
                                message.MaintenanceRequestId ==
                                maintenanceRequestId)
                        .OrderBy(message => message.CreatedAtUtc)
                        .ToArrayAsync();

                Assert.Equal(
                    "Integration Test Property",
                    savedProperty.Name);

                Assert.Equal(
                    "100 Test Street",
                    savedProperty.Address.Line1);

                Assert.NotNull(savedProperty.Location);

                Assert.Equal(
                    ownerProfileId,
                    Assert.Single(
                        savedProperty.Owners).UserProfileId);

                Assert.Equal(
                    propertyId,
                    savedRentalUnit.PropertyId);

                Assert.True(
                    savedTenancy.IsMemberActiveOn(
                        tenantProfileId,
                        new DateOnly(2026, 6, 1)));

                Assert.Equal(
                    tenancyId,
                    savedMaintenanceRequest.TenancyId);

                Assert.Equal(
                    tenantProfileId,
                    savedMaintenanceRequest
                        .CreatedByUserProfileId);

                Assert.Equal(
                    "Leaking kitchen pipe",
                    savedMaintenanceRequest.Title);

                Assert.Equal(
                    MaintenanceStatus.Resolved,
                    savedMaintenanceRequest.Status);

                Assert.Equal(
                    maintenanceCreatedAtUtc.AddDays(1),
                    savedMaintenanceRequest.ScheduledForUtc);

                Assert.Equal(
                    "The damaged pipe was replaced.",
                    savedMaintenanceRequest.ResolutionNotes);

                Assert.Equal(
                    4,
                    savedMaintenanceRequest.StatusHistory.Count);

                var savedStatusChanges =
                    savedMaintenanceRequest.StatusHistory
                        .OrderBy(change => change.ChangedAtUtc)
                        .ToArray();

                Assert.Equal(
                    new[]
                    {
                        MaintenanceStatus.Acknowledged,
                        MaintenanceStatus.Scheduled,
                        MaintenanceStatus.InProgress,
                        MaintenanceStatus.Resolved
                    },
                    savedStatusChanges.Select(change => change.ToStatus));

                Assert.All(
                    savedStatusChanges,
                    change => Assert.Equal(
                        tenantProfileId,
                        change.ChangedByUserProfileId));

                var resolvedStatusChange =
                    Assert.Single(
                        savedStatusChanges,
                        change =>
                            change.ToStatus ==
                            MaintenanceStatus.Resolved);

                Assert.Equal(
                    "The damaged pipe was replaced.",
                    resolvedStatusChange.Note);

                Assert.Equal(
                    3,
                    savedMaintenanceMessages.Length);

                var savedLandlordMessage =
                    Assert.Single(
                        savedMaintenanceMessages,
                        message =>
                            message.Id == landlordMessageId);

                Assert.Equal(
                    ownerProfileId,
                    savedLandlordMessage.AuthorUserProfileId);

                Assert.Equal(
                    "A plumber has been contacted.",
                    savedLandlordMessage.Body);

                Assert.False(savedLandlordMessage.IsEdited);
                Assert.False(savedLandlordMessage.IsDeleted);

                var savedEditedMessage =
                    Assert.Single(
                        savedMaintenanceMessages,
                        message =>
                            message.Id == editedMessageId);

                Assert.Equal(
                    "The leak has slowed down.",
                    savedEditedMessage.Body);

                Assert.True(savedEditedMessage.IsEdited);
                Assert.Equal(
                    maintenanceCreatedAtUtc.AddHours(5),
                    savedEditedMessage.EditedAtUtc);

                var savedDeletedMessage =
                    Assert.Single(
                        savedMaintenanceMessages,
                        message =>
                            message.Id == deletedMessageId);

                Assert.True(savedDeletedMessage.IsDeleted);

                Assert.Equal(
                    ownerProfileId,
                    savedDeletedMessage.DeletedByUserProfileId);

                Assert.Equal(
                    maintenanceCreatedAtUtc.AddHours(7),
                    savedDeletedMessage.DeletedAtUtc);

                Assert.Equal(
                    "Message to be deleted.",
                    savedDeletedMessage.Body);

                var testClock =
                    new TestTimeProvider(
                        new DateTimeOffset(
                            2026,
                            6,
                            1,
                            12,
                            0,
                            0,
                            TimeSpan.Zero));

                var attachmentStorage =
                    new LocalMaintenanceAttachmentStorage(
                        attachmentStorageRoot);

                var maintenanceService =
                    new MaintenanceService(
                        context,
                        testClock,
                        attachmentStorage);

                var tenantTenancies =
                    await maintenanceService
                        .GetAvailableTenanciesAsync(
                            tenantProfileId);

                var roommateTenancies =
                    await maintenanceService
                        .GetAvailableTenanciesAsync(
                            roommateProfileId);

                var ownerTenancies =
                    await maintenanceService
                        .GetAvailableTenanciesAsync(
                            ownerProfileId);

                var unrelatedTenancies =
                    await maintenanceService
                        .GetAvailableTenanciesAsync(
                            unrelatedProfileId);

                var tenantTenancy =
                    Assert.Single(tenantTenancies);

                Assert.Equal(
                    tenancyId,
                    tenantTenancy.TenancyId);

                Assert.Equal(
                    "Integration Test Property",
                    tenantTenancy.PropertyName);

                Assert.Equal(
                    "Unit 101",
                    tenantTenancy.RentalUnitName);

                Assert.Equal(
                    tenancyId,
                    Assert.Single(roommateTenancies).TenancyId);

                Assert.Equal(
                    tenancyId,
                    Assert.Single(ownerTenancies).TenancyId);

                Assert.Empty(unrelatedTenancies);

                Assert.DoesNotContain(
                    tenantTenancies,
                    option =>
                        option.TenancyId == expiredTenancyId);

                Assert.DoesNotContain(
                    ownerTenancies,
                    option =>
                        option.TenancyId == expiredTenancyId);

                var tenantRequests =
                    await maintenanceService
                        .GetVisibleRequestsAsync(
                            tenantProfileId);
                var roommateRequests =
                    await maintenanceService
                        .GetVisibleRequestsAsync(
                            roommateProfileId);
                var ownerRequests =
                    await maintenanceService
                        .GetVisibleRequestsAsync(
                            ownerProfileId);
                var unrelatedRequests =
                    await maintenanceService
                        .GetVisibleRequestsAsync(
                            unrelatedProfileId);

                Assert.Equal(2, tenantRequests.Count);
                Assert.Single(roommateRequests);
                Assert.Equal(
                    maintenanceRequestId,
                    roommateRequests[0].Id);
                Assert.Equal(2, ownerRequests.Count);
                Assert.Empty(unrelatedRequests);

                Assert.Null(
                    await maintenanceService.GetDetailsAsync(
                        privateMaintenanceRequestId,
                        roommateProfileId));

                var reporterPrivateDetails =
                    await maintenanceService.GetDetailsAsync(
                        privateMaintenanceRequestId,
                        tenantProfileId);
                var ownerPrivateDetails =
                    await maintenanceService.GetDetailsAsync(
                        privateMaintenanceRequestId,
                        ownerProfileId);

                Assert.NotNull(reporterPrivateDetails);
                Assert.NotNull(ownerPrivateDetails);

                var tenantSharedDetails =
                    await maintenanceService.GetDetailsAsync(
                        maintenanceRequestId,
                        tenantProfileId);

                Assert.NotNull(tenantSharedDetails);

                var deletedMessageItem =
                    Assert.Single(
                        tenantSharedDetails.Messages,
                        message =>
                            message.Id == deletedMessageId);

                Assert.True(deletedMessageItem.IsDeleted);
                Assert.Null(deletedMessageItem.Body);

                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => maintenanceService.AddMessageAsync(
                        new AddMaintenanceMessageCommand(
                            privateMaintenanceRequestId,
                            "This message must be rejected."),
                        roommateProfileId));

                var ownerMessageId =
                    await maintenanceService.AddMessageAsync(
                        new AddMaintenanceMessageCommand(
                            privateMaintenanceRequestId,
                            "I will arrange a locksmith."),
                        ownerProfileId);

                var privateMessageNotifications =
                    await context.UserNotifications
                        .AsNoTracking()
                        .Where(notification =>
                            notification.Type ==
                                NotificationType
                                    .MaintenanceMessageAdded &&
                            notification.TargetPath ==
                                $"/maintenance/{privateMaintenanceRequestId}")
                        .ToArrayAsync();

                var privateMessageNotification =
                    Assert.Single(
                        privateMessageNotifications);

                Assert.Equal(
                    tenantProfileId,
                    privateMessageNotification
                        .RecipientUserProfileId);
                Assert.Equal(
                    ownerProfileId,
                    privateMessageNotification
                        .ActorUserProfileId);

                await maintenanceService.ChangeStatusAsync(
                    new ChangeMaintenanceStatusCommand(
                        privateMaintenanceRequestId,
                        MaintenanceAction.Acknowledge,
                        Note: "The issue has been reviewed."),
                    ownerProfileId);

                var privateStatusNotification =
                    await context.UserNotifications
                        .AsNoTracking()
                        .SingleAsync(notification =>
                            notification.Type ==
                                NotificationType
                                    .MaintenanceStatusChanged &&
                            notification.TargetPath ==
                                $"/maintenance/{privateMaintenanceRequestId}");

                Assert.Equal(
                    tenantProfileId,
                    privateStatusNotification
                        .RecipientUserProfileId);
                Assert.Equal(
                    "Private bedroom lock issue is now acknowledged.",
                    privateStatusNotification.Message);

                var updatedPrivateDetails =
                    await maintenanceService.GetDetailsAsync(
                        privateMaintenanceRequestId,
                        tenantProfileId);

                Assert.NotNull(updatedPrivateDetails);
                Assert.Equal(
                    MaintenanceStatus.Acknowledged,
                    updatedPrivateDetails.Status);
                Assert.Contains(
                    updatedPrivateDetails.Messages,
                    message => message.Id == ownerMessageId);

                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => maintenanceService.EditMessageAsync(
                        new EditMaintenanceMessageCommand(
                            ownerMessageId,
                            "A tenant cannot edit the owner's message."),
                        tenantProfileId));

                testClock.Advance(TimeSpan.FromMinutes(1));

                await maintenanceService.EditMessageAsync(
                    new EditMaintenanceMessageCommand(
                        ownerMessageId,
                        "A locksmith visit is scheduled."),
                    ownerProfileId);

                testClock.Advance(TimeSpan.FromMinutes(1));

                await maintenanceService.DeleteMessageAsync(
                    new DeleteMaintenanceMessageCommand(
                        ownerMessageId),
                    ownerProfileId);

                var detailsAfterMessageDeletion =
                    await maintenanceService.GetDetailsAsync(
                        privateMaintenanceRequestId,
                        tenantProfileId);

                Assert.NotNull(detailsAfterMessageDeletion);

                var deletedOwnerMessage =
                    Assert.Single(
                        detailsAfterMessageDeletion.Messages,
                        message => message.Id == ownerMessageId);

                Assert.True(deletedOwnerMessage.IsDeleted);
                Assert.Null(deletedOwnerMessage.Body);

                var serviceCreatedRequestId =
                    await maintenanceService.CreateAsync(
                        new CreateMaintenanceRequestCommand(
                            tenancyId,
                            "Bathroom fan stopped working",
                            "The bathroom extractor fan does not start.",
                            MaintenanceCategory.Electrical,
                            MaintenancePriority.Normal,
                            MaintenanceVisibility.SharedWithTenancy),
                        tenantProfileId);

                var requestCreatedNotification =
                    await context.UserNotifications
                        .AsNoTracking()
                        .SingleAsync(notification =>
                            notification.RecipientUserProfileId ==
                                ownerProfileId &&
                            notification.Type ==
                                NotificationType
                                    .MaintenanceRequestCreated &&
                            notification.TargetPath ==
                                $"/maintenance/{serviceCreatedRequestId}");

                Assert.Equal(
                    tenantProfileId,
                    requestCreatedNotification
                        .ActorUserProfileId);
                Assert.Equal(
                    "New maintenance request",
                    requestCreatedNotification.Title);
                Assert.Equal(
                    "Bathroom fan stopped working was submitted.",
                    requestCreatedNotification.Message);
                Assert.False(
                    requestCreatedNotification.IsRead);

                Assert.False(
                    await context.UserNotifications
                        .AnyAsync(notification =>
                            notification.RecipientUserProfileId ==
                                tenantProfileId &&
                            notification.TargetPath ==
                                $"/maintenance/{serviceCreatedRequestId}"));

                testClock.Advance(TimeSpan.FromMinutes(1));

                await maintenanceService.UpdateDetailsAsync(
                    new UpdateMaintenanceRequestCommand(
                        serviceCreatedRequestId,
                        "Bathroom extractor fan stopped working",
                        "The extractor fan no longer turns on.",
                        MaintenanceCategory.Electrical,
                        MaintenancePriority.High,
                        MaintenanceVisibility.SharedWithTenancy),
                    tenantProfileId);

                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => maintenanceService.UpdateDetailsAsync(
                        new UpdateMaintenanceRequestCommand(
                            serviceCreatedRequestId,
                            "Roommate edit must fail",
                            "A roommate cannot edit another reporter's request.",
                            MaintenanceCategory.Other,
                            MaintenancePriority.Low,
                            MaintenanceVisibility.SharedWithTenancy),
                        roommateProfileId));

                var serviceCreatedDetails =
                    await maintenanceService.GetDetailsAsync(
                        serviceCreatedRequestId,
                        roommateProfileId);

                Assert.NotNull(serviceCreatedDetails);
                Assert.Equal(
                    "Bathroom extractor fan stopped working",
                    serviceCreatedDetails.Title);
                Assert.Equal(
                    MaintenancePriority.High,
                    serviceCreatedDetails.Priority);

                var assigneeOptions = await maintenanceService.GetAssigneeOptionsAsync(
                    serviceCreatedRequestId,
                    ownerProfileId);
                Assert.Equal(
                    ownerProfileId,
                    Assert.Single(assigneeOptions).UserProfileId);

                await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                    maintenanceService.AssignAsync(
                        new AssignMaintenanceRequestCommand(
                            serviceCreatedRequestId,
                            ownerProfileId),
                        tenantProfileId));

                testClock.Advance(TimeSpan.FromMinutes(1));
                await maintenanceService.AssignAsync(
                    new AssignMaintenanceRequestCommand(
                        serviceCreatedRequestId,
                        ownerProfileId),
                    ownerProfileId);

                serviceCreatedDetails = await maintenanceService.GetDetailsAsync(
                    serviceCreatedRequestId,
                    tenantProfileId);
                Assert.NotNull(serviceCreatedDetails);
                Assert.Equal(ownerProfileId, serviceCreatedDetails.AssignedToUserProfileId);
                Assert.Equal(
                    "Integration Test Owner",
                    serviceCreatedDetails.AssignedToDisplayName);

                await using (var invalidImage =
                             new MemoryStream([1, 2, 3, 4]))
                {
                    await Assert.ThrowsAsync<ArgumentException>(
                        () => maintenanceService.AddAttachmentAsync(
                            new AddMaintenanceAttachmentCommand(
                                serviceCreatedRequestId,
                                "not-really-a-photo.png",
                                "image/png",
                                invalidImage),
                            tenantProfileId));
                }

                await using (var oversizedImage =
                             new MemoryStream(
                                 new byte[
                                     (int)MaintenanceAttachment
                                         .MaxFileSizeBytes + 1]))
                {
                    await Assert.ThrowsAsync<
                        ArgumentOutOfRangeException>(
                        () => maintenanceService.AddAttachmentAsync(
                            new AddMaintenanceAttachmentCommand(
                                serviceCreatedRequestId,
                                "oversized.png",
                                "image/png",
                                oversizedImage),
                            tenantProfileId));
                }

                byte[] pngContent =
                [
                    0x89,
                    0x50,
                    0x4E,
                    0x47,
                    0x0D,
                    0x0A,
                    0x1A,
                    0x0A,
                    0x01,
                    0x02,
                    0x03
                ];

                Guid attachmentId;

                await using (var imageContent =
                             new MemoryStream(pngContent))
                {
                    attachmentId =
                        await maintenanceService
                            .AddAttachmentAsync(
                                new AddMaintenanceAttachmentCommand(
                                    serviceCreatedRequestId,
                                    @"C:\fakepath\bathroom-fan.png",
                                    "IMAGE/PNG",
                                    imageContent),
                                tenantProfileId);
                }

                var savedAttachment =
                    await context.MaintenanceAttachments
                        .AsNoTracking()
                        .SingleAsync(
                            attachment =>
                                attachment.Id == attachmentId);

                var attachmentNotifications =
                    await context.UserNotifications
                        .AsNoTracking()
                        .Where(notification =>
                            notification.Type ==
                                NotificationType
                                    .MaintenanceAttachmentAdded &&
                            notification.TargetPath ==
                                $"/maintenance/{serviceCreatedRequestId}")
                        .ToArrayAsync();

                Assert.Equal(
                    2,
                    attachmentNotifications.Length);
                Assert.Contains(
                    attachmentNotifications,
                    notification =>
                        notification.RecipientUserProfileId ==
                            ownerProfileId);
                Assert.Contains(
                    attachmentNotifications,
                    notification =>
                        notification.RecipientUserProfileId ==
                            roommateProfileId);
                Assert.DoesNotContain(
                    attachmentNotifications,
                    notification =>
                        notification.RecipientUserProfileId ==
                            tenantProfileId);

                Assert.Equal(
                    "bathroom-fan.png",
                    savedAttachment.OriginalFileName);
                Assert.Equal(
                    "image/png",
                    savedAttachment.ContentType);
                Assert.Equal(
                    pngContent.Length,
                    savedAttachment.FileSizeBytes);

                var roommateAttachmentDetails =
                    await maintenanceService.GetDetailsAsync(
                        serviceCreatedRequestId,
                        roommateProfileId);

                Assert.NotNull(roommateAttachmentDetails);

                var roommateAttachment =
                    Assert.Single(
                        roommateAttachmentDetails.Attachments);

                Assert.Equal(
                    attachmentId,
                    roommateAttachment.Id);
                Assert.False(roommateAttachment.CanDelete);

                var tenantAttachmentDetails =
                    await maintenanceService.GetDetailsAsync(
                        serviceCreatedRequestId,
                        tenantProfileId);

                Assert.NotNull(tenantAttachmentDetails);
                Assert.True(
                    Assert.Single(
                            tenantAttachmentDetails.Attachments)
                        .CanDelete);

                var ownerDownload =
                    await maintenanceService.OpenAttachmentAsync(
                        attachmentId,
                        ownerProfileId);

                Assert.NotNull(ownerDownload);
                Assert.Equal(
                    "bathroom-fan.png",
                    ownerDownload.OriginalFileName);
                Assert.Equal(
                    "image/png",
                    ownerDownload.ContentType);

                await using (ownerDownload.Content)
                {
                    using var downloadedContent =
                        new MemoryStream();

                    await ownerDownload.Content.CopyToAsync(
                        downloadedContent);

                    Assert.Equal(
                        pngContent,
                        downloadedContent.ToArray());
                }

                Assert.Null(
                    await maintenanceService.OpenAttachmentAsync(
                        attachmentId,
                        unrelatedProfileId));

                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => maintenanceService.DeleteAttachmentAsync(
                        new DeleteMaintenanceAttachmentCommand(
                            attachmentId),
                        roommateProfileId));

                await maintenanceService.DeleteAttachmentAsync(
                    new DeleteMaintenanceAttachmentCommand(
                        attachmentId),
                    ownerProfileId);

                Assert.False(
                    await context.MaintenanceAttachments
                        .AnyAsync(
                            attachment =>
                                attachment.Id == attachmentId));

                await using var deletedContent =
                    await attachmentStorage.OpenReadAsync(
                        savedAttachment.StorageKey);

                Assert.Null(deletedContent);

                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => maintenanceService.CreateAsync(
                        new CreateMaintenanceRequestCommand(
                            tenancyId,
                            "Unauthorized request",
                            "This request must not be created.",
                            MaintenanceCategory.Other,
                            MaintenancePriority.Low,
                            MaintenanceVisibility.SharedWithTenancy),
                        unrelatedProfileId));
            }
        }
        finally
        {
            try
            {
                await using var scope =
                    serviceProvider.CreateAsyncScope();

                var cleanupContext =
                    scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();

                await cleanupContext.Database
                    .EnsureDeletedAsync();
            }
            finally
            {
                if (Directory.Exists(attachmentStorageRoot))
                {
                    Directory.Delete(
                        attachmentStorageRoot,
                        recursive: true);
                }
            }
        }
    }

    private static ServiceProvider CreateServiceProvider(
        string connectionString)
    {
        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(
            options =>
                options.UseSqlServer(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(
                options =>
                {
                    options.Stores.SchemaVersion =
                        IdentitySchemaVersions.Version3;
                })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        return services.BuildServiceProvider();
    }

    private static string CreateConnectionString()
    {
        var baseConnectionString =
            Environment.GetEnvironmentVariable(
                "RENTCONNECT_TEST_SQLSERVER")
            ??
            "Server=(localdb)\\RentConnectLocalDB;" +
            "Database=master;" +
            "Trusted_Connection=True;" +
            "MultipleActiveResultSets=True;" +
            "TrustServerCertificate=True";

        var builder =
            new SqlConnectionStringBuilder(
                baseConnectionString)
            {
                InitialCatalog =
                    $"RentConnectIntegrationTests_{Guid.NewGuid():N}"
            };

        return builder.ConnectionString;
    }

    private sealed class TestTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
