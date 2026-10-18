using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Application.Billing;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;
using RentConnect.Infrastructure.Billing;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class SharedBillServiceTests
{
    [Fact]
    public async Task Service_EnforcesTenancyAccessAndDerivesShares()
    {
        var connectionString = CreateConnectionString();
        await using var serviceProvider =
            CreateServiceProvider(connectionString);
        await using var scope =
            serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<
                IDbContextFactory<ApplicationDbContext>>();

        var ownerId = Guid.NewGuid();
        var firstTenantId = Guid.NewGuid();
        var secondTenantId = Guid.NewGuid();
        var unrelatedUserId = Guid.NewGuid();
        var tenancyId = Guid.NewGuid();
        var now = new DateTimeOffset(
            2026,
            10,
            17,
            12,
            0,
            0,
            TimeSpan.Zero);

        try
        {
            await context.Database.MigrateAsync();

            var property = new Property(
                Guid.NewGuid(),
                "Service Test Property",
                new Address(
                    "300 Test Street",
                    null,
                    "Toronto",
                    "Ontario",
                    "M3M 3M3",
                    "CA"),
                ownerId);
            var unit = new RentalUnit(
                Guid.NewGuid(),
                property.Id,
                "Unit 303",
                RentalUnitType.Apartment);
            var tenancy = new Tenancy(
                tenancyId,
                unit.Id,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31));

            tenancy.AddMember(
                Guid.NewGuid(),
                firstTenantId,
                new DateOnly(2026, 1, 1));
            tenancy.AddMember(
                Guid.NewGuid(),
                secondTenantId,
                new DateOnly(2026, 1, 1));

            context.AddRange(
                new UserProfile(ownerId, "Owner"),
                new UserProfile(firstTenantId, "First Tenant"),
                new UserProfile(secondTenantId, "Second Tenant"),
                new UserProfile(unrelatedUserId, "Unrelated User"),
                property,
                unit,
                tenancy);
            await context.SaveChangesAsync();

            var service = new SharedBillService(
                contextFactory,
                new TestTimeProvider(now));

            var billId = await service.CreateEqualSplitAsync(
                new CreateEqualSharedBillCommand(
                    tenancyId,
                    firstTenantId,
                    "October electricity",
                    SharedBillCategory.Electricity,
                    100m,
                    "CAD",
                    2,
                    new DateOnly(2026, 10, 1),
                    new DateOnly(2026, 10, 31),
                    new DateOnly(2026, 11, 15)),
                firstTenantId);

            Assert.Single(
                await service.GetVisibleBillsAsync(ownerId));
            Assert.Single(
                await service.GetVisibleBillsAsync(secondTenantId));
            Assert.Empty(
                await service.GetVisibleBillsAsync(unrelatedUserId));

            Assert.Null(
                await service.GetDetailsAsync(
                    billId,
                    unrelatedUserId));

            var creatorDetails =
                await service.GetDetailsAsync(
                    billId,
                    firstTenantId);

            Assert.NotNull(creatorDetails);
            Assert.Equal(2, creatorDetails.Shares.Count);
            Assert.Equal(
                100m,
                creatorDetails.Shares.Sum(share => share.Amount));
            Assert.True(creatorDetails.Permissions.CanPublish);

            var roommateDetails =
                await service.GetDetailsAsync(
                    billId,
                    secondTenantId);
            Assert.NotNull(roommateDetails);
            Assert.False(roommateDetails.Permissions.CanPublish);

            var ownerDetails = await service.GetDetailsAsync(
                billId,
                ownerId);
            Assert.NotNull(ownerDetails);
            Assert.True(ownerDetails.Permissions.CanPublish);

            await service.PublishAsync(
                new PublishSharedBillCommand(billId),
                ownerId);

            var partiallySettledDetails =
                await service.GetDetailsAsync(
                    billId,
                    secondTenantId);
            Assert.NotNull(partiallySettledDetails);
            Assert.Equal(
                SharedBillStatus.PartiallySettled,
                partiallySettledDetails.Status);

            await service.RaiseDisputeAsync(
                new RaiseBillDisputeCommand(
                    billId,
                    "The second share needs review."),
                secondTenantId);
            await service.DeclarePaymentAsync(
                new DeclareBillPaymentCommand(billId),
                secondTenantId);
            await service.ConfirmPaymentAsync(
                new ConfirmBillPaymentCommand(
                    billId,
                    secondTenantId),
                firstTenantId);

            var disputedDetails =
                await service.GetDetailsAsync(billId, ownerId);
            Assert.NotNull(disputedDetails);
            Assert.Equal(
                SharedBillStatus.Disputed,
                disputedDetails.Status);
            var activeDispute = Assert.Single(
                disputedDetails.Disputes);

            await service.ResolveDisputeAsync(
                new ResolveBillDisputeCommand(
                    billId,
                    activeDispute.Id,
                    "The invoice and shares were verified."),
                ownerId);

            var settledDetails =
                await service.GetDetailsAsync(
                    billId,
                    firstTenantId);
            Assert.NotNull(settledDetails);
            Assert.Equal(
                SharedBillStatus.Settled,
                settledDetails.Status);
            Assert.All(
                settledDetails.Shares,
                share => Assert.NotNull(
                    share.PaymentConfirmedAtUtc));
            Assert.True(
                Assert.Single(settledDetails.Disputes)
                    .ResolvedAtUtc.HasValue);

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CreateEqualSplitAsync(
                    new CreateEqualSharedBillCommand(
                        tenancyId,
                        unrelatedUserId,
                        "Invalid bill",
                        SharedBillCategory.Other,
                        30m,
                        "CAD",
                        2,
                        new DateOnly(2026, 10, 1),
                        new DateOnly(2026, 10, 31),
                        new DateOnly(2026, 11, 15)),
                    firstTenantId));
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

        var builder = new SqlConnectionStringBuilder(
            baseConnectionString)
        {
            InitialCatalog =
                $"RentConnectSharedBillServiceTests_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }

    private sealed class TestTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
