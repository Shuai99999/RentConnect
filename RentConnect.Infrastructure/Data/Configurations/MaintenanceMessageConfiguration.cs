using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class MaintenanceMessageConfiguration
    : IEntityTypeConfiguration<MaintenanceMessage>
{
    public void Configure(
        EntityTypeBuilder<MaintenanceMessage> builder)
    {
        builder.ToTable(
            "MaintenanceMessages",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_MaintenanceMessages_Deletion",
                    """
                    ([DeletedAtUtc] IS NULL AND [DeletedByUserProfileId] IS NULL)
                    OR
                    ([DeletedAtUtc] IS NOT NULL AND [DeletedByUserProfileId] IS NOT NULL)
                    """);

                table.HasCheckConstraint(
                    "CK_MaintenanceMessages_EditedAfterCreation",
                    """
                    [EditedAtUtc] IS NULL
                    OR [EditedAtUtc] > [CreatedAtUtc]
                    """);
            });

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Body)
            .HasMaxLength(
                MaintenanceMessage.MaxBodyLength)
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(message => message.EditedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(message => message.DeletedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<MaintenanceRequest>()
            .WithMany()
            .HasForeignKey(
                message =>
                    message.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(
                message =>
                    message.AuthorUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(
                message =>
                    message.DeletedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(
            message => new
            {
                message.MaintenanceRequestId,
                message.CreatedAtUtc
            });

        builder.HasIndex(
            message =>
                message.AuthorUserProfileId);

        builder.HasIndex(
            message =>
                message.DeletedByUserProfileId);
    }
}