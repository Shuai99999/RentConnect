using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Application.Listings;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;
using RentConnect.Infrastructure.Listings;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class RentalListingServiceTests
{
    [Fact]
    public async Task Service_CompletesPublishApplyAndApproveWorkflow()
    {
        var connectionString = CreateConnectionString();
        await using var provider = CreateServiceProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var ownerId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var secondApplicantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var availableFrom = new DateOnly(2026, 11, 1);

        try
        {
            await context.Database.MigrateAsync();
            AddUser(context, ownerId, "Landlord", "owner@example.com");
            AddUser(context, applicantId, "First Applicant", "first@example.com");
            AddUser(context, secondApplicantId, "Second Applicant", "second@example.com");
            context.Properties.Add(new Property(
                propertyId, "Campus House",
                new Address("10 College Road", null, "Calgary", "Alberta", "T2A 1A1", "CA"),
                ownerId));
            context.RentalUnits.Add(new RentalUnit(unitId, propertyId, "Garden Suite", RentalUnitType.Apartment));
            await context.SaveChangesAsync();

            var service = new RentalListingService(factory, new TestTimeProvider(now));
            var listingId = await service.PublishAsync(
                new PublishListingCommand(unitId, "Sunny garden suite", "Walk to campus.", 1250m, availableFrom),
                ownerId);

            var publicListing = Assert.Single(await service.GetPublishedAsync());
            Assert.Equal(listingId, publicListing.Id);
            Assert.Equal(1250m, publicListing.MonthlyRent);
            Assert.True((await service.GetDetailsAsync(listingId, ownerId))!.CanManage);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(
                new ApplyForListingCommand(listingId, "Owner application"), ownerId));

            await service.ApplyAsync(new ApplyForListingCommand(listingId, "I can move in November."), applicantId);
            await service.ApplyAsync(new ApplyForListingCommand(listingId, "Also interested."), secondApplicantId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(
                new ApplyForListingCommand(listingId, "Duplicate"), applicantId));

            var applications = await service.GetManagedApplicationsAsync(ownerId);
            Assert.Equal(2, applications.Count);
            Assert.All(applications, application => Assert.Equal(RentalApplicationStatus.Pending, application.Status));
            Assert.Equal(2, await context.UserNotifications.CountAsync(notification =>
                notification.RecipientUserProfileId == ownerId &&
                notification.Type == NotificationType.RentalApplicationSubmitted));

            var selectedApplication = applications.Single(application => application.ApplicantUserProfileId == applicantId);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RejectAsync(selectedApplication.Id, applicantId));
            var tenancyId = await service.ApproveAsync(selectedApplication.Id, ownerId);

            context.ChangeTracker.Clear();
            var tenancy = await context.Tenancies.Include(item => item.Members)
                .SingleAsync(item => item.Id == tenancyId);
            Assert.Equal(availableFrom, tenancy.StartDate);
            Assert.Equal(applicantId, Assert.Single(tenancy.Members).UserProfileId);
            Assert.False((await context.RentalListings.SingleAsync(item => item.Id == listingId)).IsPublished);
            Assert.Empty(await service.GetPublishedAsync());

            var resolved = await service.GetManagedApplicationsAsync(ownerId);
            Assert.Equal(RentalApplicationStatus.Approved,
                resolved.Single(application => application.ApplicantUserProfileId == applicantId).Status);
            Assert.Equal(RentalApplicationStatus.Rejected,
                resolved.Single(application => application.ApplicantUserProfileId == secondApplicantId).Status);
            Assert.Equal(1, await context.UserNotifications.CountAsync(notification =>
                notification.RecipientUserProfileId == applicantId &&
                notification.Type == NotificationType.RentalApplicationApproved));
            Assert.Equal(1, await context.UserNotifications.CountAsync(notification =>
                notification.RecipientUserProfileId == secondApplicantId &&
                notification.Type == NotificationType.RentalApplicationRejected));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static void AddUser(ApplicationDbContext context, Guid profileId, string displayName, string email)
    {
        var profile = new UserProfile(profileId, displayName);
        context.UserProfiles.Add(profile);
        context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = email, NormalizedUserName = email.ToUpperInvariant(),
            Email = email, NormalizedEmail = email.ToUpperInvariant(), EmailConfirmed = true,
            UserProfileId = profileId, UserProfile = profile, SecurityStamp = Guid.NewGuid().ToString()
        });
    }

    private static ServiceProvider CreateServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }

    private static string CreateConnectionString()
    {
        var source = Environment.GetEnvironmentVariable("RENTCONNECT_TEST_SQLSERVER") ??
                     "Server=(localdb)\\RentConnectLocalDB;Database=master;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
        return new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = $"RentConnectRentalListingServiceTests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
