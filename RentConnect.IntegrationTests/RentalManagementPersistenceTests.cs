using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;

namespace RentConnect.IntegrationTests;

public sealed class RentalManagementPersistenceTests
{
    [Fact]
    public async Task CanPersistAndReloadRentalManagementGraph()
    {
        var connectionString = CreateConnectionString();

        await using var serviceProvider =
            CreateServiceProvider(connectionString);

        var ownerProfileId = Guid.NewGuid();
        var tenantProfileId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var rentalUnitId = Guid.NewGuid();
        var tenancyId = Guid.NewGuid();
        var maintenanceRequestId = Guid.NewGuid();
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
                    tenant);

                context.Properties.Add(property);
                context.RentalUnits.Add(rentalUnit);
                context.Tenancies.Add(tenancy);
                context.MaintenanceRequests.Add(
                    maintenanceRequest);
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
            }
        }
        finally
        {
            await using var scope =
                serviceProvider.CreateAsyncScope();

            var cleanupContext =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            await cleanupContext.Database
                .EnsureDeletedAsync();
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
}
