using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class RentalApplicationConfiguration : IEntityTypeConfiguration<RentalApplication>
{
    public void Configure(EntityTypeBuilder<RentalApplication> builder)
    {
        builder.ToTable("RentalApplications", table =>
        {
            table.HasCheckConstraint("CK_RentalApplications_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_RentalApplications_Resolution", "([Status] = 1 AND [ResolvedAtUtc] IS NULL) OR ([Status] IN (2, 3) AND [ResolvedAtUtc] IS NOT NULL)");
        });
        builder.HasKey(application => application.Id);
        builder.Property(application => application.Message).HasMaxLength(RentalApplication.MaxMessageLength).IsRequired();
        builder.Property(application => application.Status).HasConversion<int>().IsRequired();
        builder.Property(application => application.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(application => application.ResolvedAtUtc).HasColumnType("datetimeoffset");
        builder.HasOne<RentalListing>().WithMany().HasForeignKey(application => application.RentalListingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(application => application.ApplicantUserProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(application => new { application.RentalListingId, application.ApplicantUserProfileId }).IsUnique();
        builder.HasIndex(application => new { application.ApplicantUserProfileId, application.CreatedAtUtc });
    }
}
