using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class UserNotificationConfiguration
    : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(
        EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable(
            "UserNotifications",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_UserNotifications_Type",
                    "[Type] IN (1, 2, 3, 4, 5, 6, 7)");

                table.HasCheckConstraint(
                    "CK_UserNotifications_ReadAfterCreation",
                    """
                    [ReadAtUtc] IS NULL
                    OR
                    [ReadAtUtc] >= [CreatedAtUtc]
                    """);

                table.HasCheckConstraint(
                    "CK_UserNotifications_LocalTarget",
                    """
                    [TargetPath] LIKE '/%'
                    AND
                    [TargetPath] NOT LIKE '//%'
                    """);
            });

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Type)
            .HasConversion<int>();

        builder.Property(notification => notification.Title)
            .HasMaxLength(
                UserNotification.MaxTitleLength)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(
                UserNotification.MaxMessageLength)
            .IsRequired();

        builder.Property(notification => notification.TargetPath)
            .HasMaxLength(
                UserNotification.MaxTargetPathLength)
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(notification => notification.ReadAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(
                notification =>
                    notification.RecipientUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(
                notification =>
                    notification.ActorUserProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(
            notification => new
            {
                notification.RecipientUserProfileId,
                notification.ReadAtUtc,
                notification.CreatedAtUtc
            });

        builder.HasIndex(
            notification =>
                notification.ActorUserProfileId);
    }
}
