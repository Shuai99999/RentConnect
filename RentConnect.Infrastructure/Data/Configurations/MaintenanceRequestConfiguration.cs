using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class MaintenanceRequestConfiguration
    : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(
        EntityTypeBuilder<MaintenanceRequest> builder)
    {
        builder.ToTable(
            "MaintenanceRequests",
            table => table.HasCheckConstraint(
                "CK_MaintenanceRequests_UpdateTime",
                "[UpdatedAtUtc] >= [CreatedAtUtc]"));

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Title)
            .HasMaxLength(
                MaintenanceRequest.MaxTitleLength)
            .IsRequired();

        builder.Property(request => request.Description)
            .HasMaxLength(
                MaintenanceRequest.MaxDescriptionLength)
            .IsRequired();

        builder.Property(request => request.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(request => request.Priority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(request => request.Visibility)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(request => request.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(request => request.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(request => request.UpdatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(request => request.ScheduledForUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(request => request.ResolutionNotes)
            .HasMaxLength(
                MaintenanceRequest.MaxResolutionNotesLength);

        builder.Property(request => request.AssignedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<Tenancy>()
            .WithMany()
            .HasForeignKey(request => request.TenancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(
                request =>
                    request.CreatedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(request => request.AssignedToUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => new
        {
            request.TenancyId,
            request.CreatedAtUtc
        });

        builder.HasIndex(request => new
        {
            request.TenancyId,
            request.Status
        });

        builder.HasIndex(
            request =>
                request.CreatedByUserProfileId);

        builder.HasIndex(request => request.AssignedToUserProfileId);

        builder.Navigation(
        request => request.StatusHistory)
            .HasField("_statusHistory")
            .UsePropertyAccessMode(
        PropertyAccessMode.Field);
    }
}
