using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Application.Tenancies;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;
using RentConnect.Infrastructure.Tenancies;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class TenancyServiceTests
{
    [Fact]
    public async Task Service_ManagesTenancyAndSecureInvitationLifecycle()
    {
        var connectionString = CreateConnectionString();
        await using var provider = CreateServiceProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var unrelatedId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        try
        {
            await context.Database.MigrateAsync();
            AddUser(context, ownerId, "Owner", "owner@example.com");
            AddUser(context, tenantId, "Tenant", "tenant@example.com");
            AddUser(context, unrelatedId, "Other", "other@example.com");
            context.Properties.Add(new Property(
                propertyId,
                "Maple House",
                new Address("100 Maple Avenue", null, "Red Deer", "Alberta", "T4N 1A1", "CA"),
                ownerId));
            context.RentalUnits.Add(new RentalUnit(unitId, propertyId, "Garden Suite", RentalUnitType.Apartment));
            await context.SaveChangesAsync();

            var service = new TenancyService(factory, new TestTimeProvider(now));
            var tenancyId = await service.CreateAsync(
                new CreateTenancyCommand(unitId, new DateOnly(2026, 11, 1), new DateOnly(2027, 10, 31)), ownerId);

            Assert.Null(await service.GetDetailsAsync(tenancyId, unrelatedId));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
                new CreateTenancyCommand(unitId, new DateOnly(2027, 1, 1), null), ownerId));

            var firstInvite = await service.InviteAsync(
                new InviteTenancyMemberCommand(tenancyId, "tenant@example.com", new DateOnly(2026, 11, 1)), ownerId);
            var secondInvite = await service.InviteAsync(
                new InviteTenancyMemberCommand(tenancyId, "tenant@example.com", new DateOnly(2026, 11, 1)), ownerId);

            Assert.False((await service.GetInvitationAsync(firstInvite.Token))!.IsAvailable);
            Assert.True((await service.GetInvitationAsync(secondInvite.Token))!.IsAvailable);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AcceptInvitationAsync(
                secondInvite.Token, unrelatedId, "other@example.com"));

            var acceptedTenancyId = await service.AcceptInvitationAsync(
                secondInvite.Token, tenantId, "Tenant@Example.com");
            Assert.Equal(tenancyId, acceptedTenancyId);
            Assert.False((await service.GetInvitationAsync(secondInvite.Token))!.IsAvailable);

            var tenantSummary = Assert.Single(await service.GetVisibleAsync(tenantId));
            Assert.False(tenantSummary.CanManage);
            var details = await service.GetDetailsAsync(tenancyId, ownerId);
            Assert.NotNull(details);
            Assert.True(details.CanManage);
            Assert.Equal("Tenant", Assert.Single(details.Members).DisplayName);
            Assert.Equal(2, details.Invitations.Count);

            await service.MarkMemberLeftAsync(
                new MarkTenancyMemberLeftCommand(tenancyId, tenantId, new DateOnly(2027, 1, 31)), ownerId);
            details = await service.GetDetailsAsync(tenancyId, ownerId);
            Assert.Equal(new DateOnly(2027, 1, 31), Assert.Single(details!.Members).LeftOn);
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
            InitialCatalog = $"RentConnectTenancyServiceTests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
