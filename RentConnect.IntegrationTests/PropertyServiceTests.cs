using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Application.Properties;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Data;
using RentConnect.Infrastructure.Properties;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class PropertyServiceTests
{
    [Fact]
    public async Task Service_ManagesOwnedPropertiesAndLimitsTenantVisibility()
    {
        var connectionString = CreateConnectionString();
        await using var serviceProvider =
            CreateServiceProvider(connectionString);
        await using var scope = serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var unrelatedId = Guid.NewGuid();
        var today = new DateOnly(2026, 10, 18);

        try
        {
            await context.Database.MigrateAsync();
            context.UserProfiles.AddRange(
                new UserProfile(ownerId, "Property Owner"),
                new UserProfile(tenantId, "Current Tenant"),
                new UserProfile(unrelatedId, "Unrelated User"));
            await context.SaveChangesAsync();

            var service = new PropertyService(
                contextFactory,
                new TestTimeProvider(
                    new DateTimeOffset(
                        2026, 10, 18, 12, 0, 0, TimeSpan.Zero)));

            var propertyId = await service.CreateAsync(
                new CreatePropertyCommand(
                    "Maple House",
                    "100 Maple Avenue",
                    null,
                    "Red Deer",
                    "Alberta",
                    "T4N 1A1",
                    "CA"),
                ownerId);
            var occupiedUnitId = await service.AddRentalUnitAsync(
                new AddRentalUnitCommand(
                    propertyId,
                    "Main Floor",
                    RentalUnitType.Apartment),
                ownerId);
            var vacantUnitId = await service.AddRentalUnitAsync(
                new AddRentalUnitCommand(
                    propertyId,
                    "Garden Suite",
                    RentalUnitType.Apartment),
                ownerId);

            var tenancy = new Tenancy(
                Guid.NewGuid(),
                occupiedUnitId,
                today.AddMonths(-2),
                today.AddMonths(10));
            tenancy.AddMember(Guid.NewGuid(), tenantId, today.AddMonths(-2));
            context.Tenancies.Add(tenancy);
            var listing = new RentalListing(
                Guid.NewGuid(),
                vacantUnitId,
                "Garden suite",
                "Private lower-level suite.",
                1200m,
                today,
                new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
            context.RentalListings.Add(listing);
            await context.SaveChangesAsync();

            var ownerSummary = Assert.Single(
                await service.GetVisiblePropertiesAsync(ownerId));
            Assert.True(ownerSummary.CanManage);
            Assert.Equal(2, ownerSummary.VisibleUnitCount);
            Assert.Equal(1, ownerSummary.ActiveTenancyCount);

            var tenantSummary = Assert.Single(
                await service.GetVisiblePropertiesAsync(tenantId));
            Assert.False(tenantSummary.CanManage);
            Assert.Equal(1, tenantSummary.VisibleUnitCount);
            Assert.Empty(
                await service.GetVisiblePropertiesAsync(unrelatedId));

            var tenantDetails = await service.GetDetailsAsync(
                propertyId,
                tenantId);
            Assert.NotNull(tenantDetails);
            Assert.False(tenantDetails.CanManage);
            var visibleTenantUnit = Assert.Single(tenantDetails.Units);
            Assert.Equal(occupiedUnitId, visibleTenantUnit.Id);
            Assert.True(visibleTenantUnit.HasActiveTenancy);
            Assert.Equal(1, visibleTenantUnit.CurrentTenantCount);

            var ownerDetails = await service.GetDetailsAsync(
                propertyId,
                ownerId);
            Assert.NotNull(ownerDetails);
            Assert.True(ownerDetails.CanManage);
            Assert.Equal(2, ownerDetails.Units.Count);
            Assert.Equal("Property Owner", Assert.Single(ownerDetails.OwnerNames));
            var listedUnit = ownerDetails.Units.Single(unit =>
                unit.Id == vacantUnitId);
            Assert.Equal(listing.Id, listedUnit.ListingId);
            Assert.True(listedUnit.IsListingPublished);

            await service.UpdateAsync(
                new UpdatePropertyCommand(
                    propertyId,
                    "Maple House Updated",
                    "101 Maple Avenue",
                    "Building A",
                    "Red Deer",
                    "Alberta",
                    "T4N 1A2",
                    "CA"),
                ownerId);
            await service.UpdateRentalUnitAsync(
                new UpdateRentalUnitCommand(
                    vacantUnitId,
                    "Lower Garden Suite",
                    RentalUnitType.EntireProperty),
                ownerId);
            await service.SetRentalUnitActiveAsync(
                new SetRentalUnitActiveCommand(vacantUnitId, false),
                ownerId);

            var updated = await service.GetDetailsAsync(propertyId, ownerId);
            Assert.NotNull(updated);
            Assert.Equal("Maple House Updated", updated.Name);
            Assert.Equal("101 Maple Avenue", updated.AddressLine1);
            var updatedUnit = updated.Units.Single(unit =>
                unit.Id == vacantUnitId);
            Assert.Equal("Lower Garden Suite", updatedUnit.Name);
            Assert.Equal(RentalUnitType.EntireProperty, updatedUnit.Type);
            Assert.False(updatedUnit.IsActive);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SetRentalUnitActiveAsync(
                    new SetRentalUnitActiveCommand(occupiedUnitId, false),
                    ownerId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.UpdateAsync(
                    new UpdatePropertyCommand(
                        propertyId,
                        "Unauthorized",
                        "1 Hidden Street",
                        null,
                        "Red Deer",
                        "Alberta",
                        null,
                        "CA"),
                    tenantId));
            Assert.Null(await service.GetDetailsAsync(propertyId, unrelatedId));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static ServiceProvider CreateServiceProvider(
        string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(
            options => options.UseSqlServer(connectionString));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }

    private static string CreateConnectionString()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(
                "RENTCONNECT_TEST_SQLSERVER")
            ?? "Server=(localdb)\\RentConnectLocalDB;" +
               "Database=master;" +
               "Trusted_Connection=True;" +
               "MultipleActiveResultSets=True;" +
               "TrustServerCertificate=True";
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"RentConnectPropertyServiceTests_{Guid.NewGuid():N}"
        };
        return builder.ConnectionString;
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
