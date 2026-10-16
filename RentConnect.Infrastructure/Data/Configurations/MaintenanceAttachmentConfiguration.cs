using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class MaintenanceAttachmentConfiguration
    : IEntityTypeConfiguration<MaintenanceAttachment>
{
    public void Configure(
        EntityTypeBuilder<MaintenanceAttachment> builder)
    {
        builder.ToTable(
            "MaintenanceAttachments",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_MaintenanceAttachments_FileSize",
                    $"""
                    [FileSizeBytes] > 0
                    AND
                    [FileSizeBytes] <= {MaintenanceAttachment.MaxFileSizeBytes}
                    """);
            });

        builder.HasKey(attachment => attachment.Id);

        builder.Property(
                attachment =>
                    attachment.OriginalFileName)
            .HasMaxLength(
                MaintenanceAttachment
                    .MaxOriginalFileNameLength)
            .IsRequired();

        builder.Property(
                attachment =>
                    attachment.ContentType)
            .HasMaxLength(
                MaintenanceAttachment
                    .MaxContentTypeLength)
            .IsRequired();

        builder.Property(
                attachment =>
                    attachment.StorageKey)
            .HasMaxLength(
                MaintenanceAttachment
                    .MaxStorageKeyLength)
            .IsRequired();

        builder.Property(
                attachment =>
                    attachment.UploadedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<MaintenanceRequest>()
            .WithMany()
            .HasForeignKey(
                attachment =>
                    attachment.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(
                attachment =>
                    attachment.UploadedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(
            attachment =>
                attachment.StorageKey)
            .IsUnique();

        builder.HasIndex(
            attachment => new
            {
                attachment.MaintenanceRequestId,
                attachment.UploadedAtUtc
            });

        builder.HasIndex(
            attachment =>
                attachment.UploadedByUserProfileId);
    }
}