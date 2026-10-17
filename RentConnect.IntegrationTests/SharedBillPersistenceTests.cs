using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class SharedBillPersistenceTests
{
    [Fact]
    public async Task CanPersistAndReloadSharedBillSettlementGraph()
    {
        var connectionString = CreateConnectionString();

        await using var serviceProvider =
            CreateServiceProvider(connectionString);

        await using var scope =
            serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var ownerProfileId = Guid.NewGuid();
        var firstTenantProfileId = Guid.NewGuid();
        var secondTenantProfileId = Guid.NewGuid();
        var tenancyId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();

        var createdAtUtc =
            new DateTimeOffset(
                2026,
                10,
                1,
                12,
                0,
                0,
                TimeSpan.Zero);

        try
        {
            await context.Database.MigrateAsync();

            var owner = new UserProfile(
                ownerProfileId,
                "Bill Test Owner");
            var firstTenant = new UserProfile(
                firstTenantProfileId,
                "First Bill Test Tenant");
            var secondTenant = new UserProfile(
                secondTenantProfileId,
                "Second Bill Test Tenant");

            var property = new Property(
                Guid.NewGuid(),
                "Bill Test Property",
                new Address(
                    "200 Test Street",
                    null,
                    "Toronto",
                    "Ontario",
                    "M2M 2M2",
                    "CA"),
                ownerProfileId);

            var rentalUnit = new RentalUnit(
                Guid.NewGuid(),
                property.Id,
                "Unit 202",
                RentalUnitType.Apartment);

            var tenancy = new Tenancy(
                tenancyId,
                rentalUnit.Id,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31));

            tenancy.AddMember(
                Guid.NewGuid(),
                firstTenantProfileId,
                new DateOnly(2026, 1, 1));
            tenancy.AddMember(
                Guid.NewGuid(),
                secondTenantProfileId,
                new DateOnly(2026, 1, 1));

            var bill = new SharedBill(
                billId,
                tenancyId,
                firstTenantProfileId,
                firstTenantProfileId,
                "October electricity",
                SharedBillCategory.Electricity,
                new Money(100m, "CAD"),
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 31),
                new DateOnly(2026, 11, 15),
                createdAtUtc);

            bill.AllocateEqually(
                [
                    (Guid.NewGuid(), firstTenantProfileId),
                    (Guid.NewGuid(), secondTenantProfileId)
                ],
                decimalPlaces: 2,
                createdAtUtc.AddMinutes(1));
            bill.Publish(createdAtUtc.AddMinutes(2));

            context.AddRange(
                owner,
                firstTenant,
                secondTenant,
                property,
                rentalUnit,
                tenancy,
                bill);

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var reloadedBill = await context.SharedBills
                .Include(item => item.Shares)
                .Include(item => item.Disputes)
                .SingleAsync(item => item.Id == billId);

            Assert.Equal(100m, reloadedBill.Total.Amount);
            Assert.Equal("CAD", reloadedBill.Total.Currency);
            Assert.Equal(2, reloadedBill.Shares.Count);
            Assert.Equal(
                100m,
                reloadedBill.Shares.Sum(
                    share => share.Amount.Amount));

            reloadedBill.DeclareSharePayment(
                firstTenantProfileId,
                createdAtUtc.AddMinutes(3));
            reloadedBill.ConfirmSharePayment(
                firstTenantProfileId,
                createdAtUtc.AddMinutes(4));
            reloadedBill.RaiseDispute(
                disputeId,
                secondTenantProfileId,
                "The invoice needs to be checked.",
                createdAtUtc.AddMinutes(5));
            reloadedBill.DeclareSharePayment(
                secondTenantProfileId,
                createdAtUtc.AddMinutes(6));
            reloadedBill.ConfirmSharePayment(
                secondTenantProfileId,
                createdAtUtc.AddMinutes(7));

            Assert.Equal(
                SharedBillStatus.Disputed,
                reloadedBill.Status);

            reloadedBill.ResolveDispute(
                disputeId,
                ownerProfileId,
                "The invoice was verified.",
                createdAtUtc.AddMinutes(8));

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var settledBill = await context.SharedBills
                .AsNoTracking()
                .Include(item => item.Shares)
                .Include(item => item.Disputes)
                .SingleAsync(item => item.Id == billId);

            Assert.Equal(SharedBillStatus.Settled, settledBill.Status);
            Assert.All(
                settledBill.Shares,
                share =>
                {
                    Assert.True(share.IsPaymentDeclared);
                    Assert.True(share.IsSettled);
                });

            var settledDispute = Assert.Single(settledBill.Disputes);
            Assert.True(settledDispute.IsResolved);
            Assert.Equal(
                ownerProfileId,
                settledDispute.ResolvedByUserProfileId);
            Assert.Equal(
                "The invoice was verified.",
                settledDispute.Resolution);
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

        services.AddDbContext<ApplicationDbContext>(
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
                $"RentConnectSharedBillTests_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }
}
