using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class MaintenanceStatusChangeConfiguration
    : IEntityTypeConfiguration<MaintenanceStatusChange>
{
    public void Configure(
        EntityTypeBuilder<MaintenanceStatusChange> builder)
    {
        builder.ToTable(
            "MaintenanceStatusHistory",
            table => table.HasCheckConstraint(
                "CK_MaintenanceStatusHistory_StatusChange",
                "[FromStatus] <> [ToStatus]"));

        builder.HasKey(change => change.Id);

        builder.Property(change => change.FromStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(change => change.ToStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(change => change.ChangedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(change => change.Note)
            .HasMaxLength(
                MaintenanceStatusChange.MaxNoteLength);

        builder.HasOne<MaintenanceRequest>()
            .WithMany(request => request.StatusHistory)
            .HasForeignKey(
                change => change.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(
                change => change.ChangedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(change => new
        {
            change.MaintenanceRequestId,
            change.ChangedAtUtc
        });

        builder.HasIndex(
            change =>
                change.ChangedByUserProfileId);
    }
}