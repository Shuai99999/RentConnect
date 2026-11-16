using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;
using RentConnect.Infrastructure.Listings;
using RentConnect.Infrastructure.Properties;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class RentalUnitImageServiceTests
{
    private static readonly byte[] FirstPng =
        [137, 80, 78, 71, 13, 10, 26, 10, 1];

    private static readonly byte[] SecondPng =
        [137, 80, 78, 71, 13, 10, 26, 10, 2];

    [Fact]
    public async Task Images_AreIsolatedByRentalUnitAndUsedAsListingCovers()
    {
        await using var provider = CreateServiceProvider(CreateConnectionString());
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var firstUnitId = Guid.NewGuid();
        var secondUnitId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

        try
        {
            await context.Database.MigrateAsync();
            AddUser(context, ownerId, "Owner", "owner-images@example.com");
            AddUser(context, viewerId, "Viewer", "viewer-images@example.com");

            context.Properties.Add(new Property(
                propertyId,
                "Two Suite House",
                new Address(
                    "10 Test Street",
                    null,
                    "Edmonton",
                    "Alberta",
                    "T5A 1A1",
                    "CA"),
                ownerId));
            context.RentalUnits.AddRange(
                new RentalUnit(
                    firstUnitId,
                    propertyId,
                    "Main Floor",
                    RentalUnitType.Apartment),
                new RentalUnit(
                    secondUnitId,
                    propertyId,
                    "Basement",
                    RentalUnitType.Apartment));
            context.RentalListings.AddRange(
                new RentalListing(
                    Guid.NewGuid(),
                    firstUnitId,
                    "Main floor listing",
                    "Main floor description",
                    1500m,
                    new DateOnly(2026, 12, 1),
                    now),
                new RentalListing(
                    Guid.NewGuid(),
                    secondUnitId,
                    "Basement listing",
                    "Basement description",
                    1000m,
                    new DateOnly(2026, 12, 1),
                    now));
            await context.SaveChangesAsync();

            var clock = new FixedTimeProvider(now);
            var images = new RentalUnitImageService(factory, clock);

            var firstImageId = await images.AddAsync(
                firstUnitId,
                ownerId,
                new MemoryStream(FirstPng));
            var secondImageId = await images.AddAsync(
                secondUnitId,
                ownerId,
                new MemoryStream(SecondPng));

            Assert.Equal(
                firstImageId,
                Assert.Single(await images.GetAsync(firstUnitId, ownerId)).Id);
            Assert.Equal(
                secondImageId,
                Assert.Single(await images.GetAsync(secondUnitId, ownerId)).Id);
            Assert.Equal(
                FirstPng,
                (await images.OpenAsync(firstImageId, viewerId))!.Content);
            Assert.Equal(
                SecondPng,
                (await images.OpenAsync(secondImageId, viewerId))!.Content);

            var listings = new RentalListingService(factory, clock);
            var published = await listings.GetPublishedAsync();
            Assert.Equal(
                firstImageId,
                published.Single(item => item.Title == "Main floor listing").CoverImageId);
            Assert.Equal(
                secondImageId,
                published.Single(item => item.Title == "Basement listing").CoverImageId);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                images.AddAsync(
                    firstUnitId,
                    viewerId,
                    new MemoryStream(FirstPng)));

            await images.DeleteAsync(firstImageId, ownerId);
            Assert.Empty(await images.GetAsync(firstUnitId, ownerId));
            Assert.Equal(
                secondImageId,
                Assert.Single(await images.GetAsync(secondUnitId, ownerId)).Id);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Migration_CopiesLegacyPropertyImagesToEachExistingRentalUnit()
    {
        await using var provider = CreateServiceProvider(CreateConnectionString());
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        var ownerId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var firstUnitId = Guid.NewGuid();
        var secondUnitId = Guid.NewGuid();
        var uploadedAt = new DateTimeOffset(
            2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        try
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20261027194955_AddPropertyImages");

            AddUser(context, ownerId, "Migration Owner", "migration-owner@example.com");
            context.Properties.Add(new Property(
                propertyId,
                "Legacy Image Property",
                new Address(
                    "20 Test Street",
                    null,
                    "Edmonton",
                    "Alberta",
                    "T5A 2A2",
                    "CA"),
                ownerId));
            context.RentalUnits.AddRange(
                new RentalUnit(
                    firstUnitId,
                    propertyId,
                    "Upper",
                    RentalUnitType.Apartment),
                new RentalUnit(
                    secondUnitId,
                    propertyId,
                    "Lower",
                    RentalUnitType.Apartment));
            await context.SaveChangesAsync();

            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO [PropertyImages]
                    ([Id], [PropertyId], [ContentType], [Content], [UploadedAtUtc])
                VALUES ({0}, {1}, {2}, {3}, {4})
                """,
                Guid.NewGuid(),
                propertyId,
                "image/png",
                FirstPng,
                uploadedAt);

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();

            var migrated = await context.RentalUnitImages
                .AsNoTracking()
                .OrderBy(image => image.RentalUnitId)
                .ToArrayAsync();

            Assert.Equal(2, migrated.Length);
            Assert.Contains(migrated, image => image.RentalUnitId == firstUnitId);
            Assert.Contains(migrated, image => image.RentalUnitId == secondUnitId);
            Assert.All(migrated, image => Assert.Equal(FirstPng, image.Content));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static void AddUser(
        ApplicationDbContext context,
        Guid profileId,
        string displayName,
        string email)
    {
        var profile = new UserProfile(profileId, displayName);
        context.UserProfiles.Add(profile);
        context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            UserProfileId = profileId,
            UserProfile = profile,
            SecurityStamp = Guid.NewGuid().ToString()
        });
    }

    private static ServiceProvider CreateServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(
            options => options.UseSqlServer(connectionString));
        services.AddIdentityCore<ApplicationUser>(
                options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }

    private static string CreateConnectionString()
    {
        var source = Environment.GetEnvironmentVariable("RENTCONNECT_TEST_SQLSERVER") ??
                     "Server=(localdb)\\RentConnectLocalDB;Database=master;" +
                     "Trusted_Connection=True;MultipleActiveResultSets=True;" +
                     "TrustServerCertificate=True";

        return new SqlConnectionStringBuilder(source)
        {
            InitialCatalog =
                $"RentConnectRentalUnitImageTests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
