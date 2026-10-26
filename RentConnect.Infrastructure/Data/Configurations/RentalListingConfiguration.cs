using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class RentalListingConfiguration : IEntityTypeConfiguration<RentalListing>
{
    public void Configure(EntityTypeBuilder<RentalListing> builder)
    {
        builder.ToTable("RentalListings", table => table.HasCheckConstraint("CK_RentalListings_MonthlyRent", "[MonthlyRent] > 0"));
        builder.HasKey(listing => listing.Id);
        builder.Property(listing => listing.Title).HasMaxLength(RentalListing.MaxTitleLength).IsRequired();
        builder.Property(listing => listing.Description).HasMaxLength(RentalListing.MaxDescriptionLength).IsRequired();
        builder.Property(listing => listing.MonthlyRent).HasPrecision(18, 2).IsRequired();
        builder.Property(listing => listing.AvailableFrom).HasColumnType("date").IsRequired();
        builder.Property(listing => listing.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(listing => listing.IsPublished).IsRequired();
        builder.HasOne<RentalUnit>().WithMany().HasForeignKey(listing => listing.RentalUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(listing => listing.RentalUnitId).IsUnique();
        builder.HasIndex(listing => new { listing.IsPublished, listing.CreatedAtUtc });
    }
}
