using System.Data;
using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Properties;
using RentConnect.Core.Entities;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Properties;

public sealed class RentalUnitImageService(
    IDbContextFactory<ApplicationDbContext> factory,
    TimeProvider clock) : IRentalUnitImageService
{
    public async Task<IReadOnlyList<RentalUnitImageItem>> GetAsync(
        Guid rentalUnitId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        return await db.RentalUnitImages
            .AsNoTracking()
            .Where(image =>
                image.RentalUnitId == rentalUnitId &&
                VisibleUnits(db, userId, today)
                    .Any(unit => unit.Id == image.RentalUnitId))
            .OrderBy(image => image.UploadedAtUtc)
            .ThenBy(image => image.Id)
            .Select(image => new RentalUnitImageItem(image.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Guid> AddAsync(
        Guid rentalUnitId,
        Guid userId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;

        while ((count = await content.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > RentalUnitImage.MaxBytes)
            {
                throw new ArgumentException("Image exceeds 5 MB.", nameof(content));
            }

            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }

        var image = new RentalUnitImage(
            rentalUnitId,
            buffer.ToArray(),
            clock.GetUtcNow());

        await using var initial = await factory.CreateDbContextAsync(cancellationToken);
        return await initial.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var canManage = await db.RentalUnits.AnyAsync(
                unit =>
                    unit.Id == rentalUnitId &&
                    db.PropertyOwners.Any(owner =>
                        owner.PropertyId == unit.PropertyId &&
                        owner.UserProfileId == userId),
                cancellationToken);

            if (!canManage)
            {
                throw new KeyNotFoundException("Rental unit not found.");
            }

            if (await db.RentalUnitImages.CountAsync(
                    item => item.RentalUnitId == rentalUnitId,
                    cancellationToken) >= 10)
            {
                throw new InvalidOperationException(
                    "A rental space can have up to 10 images.");
            }

            db.RentalUnitImages.Add(image);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return image.Id;
        });
    }

    public async Task DeleteAsync(
        Guid imageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var image = await db.RentalUnitImages.SingleOrDefaultAsync(
                item =>
                    item.Id == imageId &&
                    db.RentalUnits.Any(unit =>
                        unit.Id == item.RentalUnitId &&
                        db.PropertyOwners.Any(owner =>
                            owner.PropertyId == unit.PropertyId &&
                            owner.UserProfileId == userId)),
                cancellationToken)
            ?? throw new KeyNotFoundException("Image not found.");

        db.RentalUnitImages.Remove(image);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RentalUnitImageContent?> OpenAsync(
        Guid imageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        return await db.RentalUnitImages
            .AsNoTracking()
            .Where(image =>
                image.Id == imageId &&
                VisibleUnits(db, userId, today)
                    .Any(unit => unit.Id == image.RentalUnitId))
            .Select(image => new RentalUnitImageContent(
                image.Content,
                image.ContentType))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<RentalUnit> VisibleUnits(
        ApplicationDbContext db,
        Guid userId,
        DateOnly today) =>
        db.RentalUnits.Where(unit =>
            db.PropertyOwners.Any(owner =>
                owner.PropertyId == unit.PropertyId &&
                owner.UserProfileId == userId) ||
            unit.IsActive && db.RentalListings.Any(listing =>
                listing.RentalUnitId == unit.Id && listing.IsPublished) ||
            db.Tenancies.Any(tenancy =>
                tenancy.RentalUnitId == unit.Id &&
                !tenancy.IsCancelled &&
                tenancy.StartDate <= today &&
                (!tenancy.EndDate.HasValue || tenancy.EndDate >= today) &&
                db.TenancyMembers.Any(member =>
                    member.TenancyId == tenancy.Id &&
                    member.UserProfileId == userId &&
                    member.JoinedOn <= today &&
                    (!member.LeftOn.HasValue || member.LeftOn >= today))));
}
