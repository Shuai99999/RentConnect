using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class BillAttachmentConfiguration : IEntityTypeConfiguration<BillAttachment>
{
    public void Configure(EntityTypeBuilder<BillAttachment> builder)
    {
        builder.ToTable(
            "BillAttachments",
            table => table.HasCheckConstraint(
                "CK_BillAttachments_FileSize",
                $"[FileSizeBytes] > 0 AND [FileSizeBytes] <= {BillAttachment.MaxFileSizeBytes}"));

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).ValueGeneratedNever();
        builder.Property(attachment => attachment.OriginalFileName)
            .HasMaxLength(BillAttachment.MaxOriginalFileNameLength).IsRequired();
        builder.Property(attachment => attachment.ContentType)
            .HasMaxLength(BillAttachment.MaxContentTypeLength).IsRequired();
        builder.Property(attachment => attachment.StorageKey)
            .HasMaxLength(BillAttachment.MaxStorageKeyLength).IsRequired();
        builder.Property(attachment => attachment.UploadedAtUtc)
            .HasColumnType("datetimeoffset").IsRequired();

        builder.HasOne<SharedBill>().WithMany()
            .HasForeignKey(attachment => attachment.SharedBillId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>().WithMany()
            .HasForeignKey(attachment => attachment.UploadedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attachment => attachment.StorageKey).IsUnique();
        builder.HasIndex(attachment => new { attachment.SharedBillId, attachment.UploadedAtUtc });
        builder.HasIndex(attachment => attachment.UploadedByUserProfileId);
    }
}
