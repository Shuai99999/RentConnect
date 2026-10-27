using System.Data;
using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Properties;
using RentConnect.Core.Entities;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Properties;

public sealed class PropertyImageService(IDbContextFactory<ApplicationDbContext> factory, TimeProvider clock) : IPropertyImageService
{
    private static IQueryable<Property> Visible(ApplicationDbContext db, Guid userId, DateOnly today) =>
        db.Properties.Where(p => db.PropertyOwners.Any(o => o.PropertyId == p.Id && o.UserProfileId == userId)
            || db.RentalUnits.Any(u => u.PropertyId == p.Id && (u.IsActive && db.RentalListings.Any(l => l.RentalUnitId == u.Id && l.IsPublished)
                || db.Tenancies.Any(t => t.RentalUnitId == u.Id && !t.IsCancelled && t.StartDate <= today && (!t.EndDate.HasValue || t.EndDate >= today)
                    && db.TenancyMembers.Any(m => m.TenancyId == t.Id && m.UserProfileId == userId && m.JoinedOn <= today && (!m.LeftOn.HasValue || m.LeftOn >= today))))));
    public async Task<IReadOnlyList<PropertyImageItem>> GetAsync(Guid propertyId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return await db.PropertyImages.AsNoTracking().Where(i => i.PropertyId == propertyId && Visible(db,userId,today).Any(p => p.Id == i.PropertyId))
            .OrderBy(i => i.UploadedAtUtc).ThenBy(i => i.Id).Select(i => new PropertyImageItem(i.Id)).ToArrayAsync(cancellationToken);
    }
    public async Task<Guid> AddAsync(Guid propertyId, Guid userId, Stream content, CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920]; int count;
        while ((count = await content.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > PropertyImage.MaxBytes) throw new ArgumentException("Image exceeds 5 MB.");
            buffer.Write(chunk,0,count);
        }
        var image = new PropertyImage(propertyId, buffer.ToArray(), clock.GetUtcNow());
        await using var initial = await factory.CreateDbContextAsync(cancellationToken);
        return await initial.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,cancellationToken);
            if (!await db.PropertyOwners.AnyAsync(o => o.PropertyId == propertyId && o.UserProfileId == userId,cancellationToken)) throw new KeyNotFoundException("Property not found.");
            if (await db.PropertyImages.CountAsync(i => i.PropertyId == propertyId,cancellationToken) >= 10) throw new InvalidOperationException("A property can have up to 10 images.");
            db.PropertyImages.Add(image); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return image.Id;
        });
    }
    public async Task DeleteAsync(Guid imageId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var image = await db.PropertyImages.SingleOrDefaultAsync(i => i.Id == imageId && db.PropertyOwners.Any(o => o.PropertyId == i.PropertyId && o.UserProfileId == userId),cancellationToken)
            ?? throw new KeyNotFoundException("Image not found.");
        db.PropertyImages.Remove(image); await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<PropertyImageContent?> OpenAsync(Guid imageId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return await db.PropertyImages.AsNoTracking().Where(i => i.Id == imageId && Visible(db,userId,today).Any(p => p.Id == i.PropertyId))
            .Select(i => new PropertyImageContent(i.Content,i.ContentType)).SingleOrDefaultAsync(cancellationToken);
    }
}
