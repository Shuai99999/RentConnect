using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class BillDisputeConfiguration
    : IEntityTypeConfiguration<BillDispute>
{
    public void Configure(EntityTypeBuilder<BillDispute> builder)
    {
        builder.ToTable(
            "BillDisputes",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_BillDisputes_ResolutionTime",
                    "[ResolvedAtUtc] IS NULL OR " +
                    "[ResolvedAtUtc] >= [RaisedAtUtc]");
                table.HasCheckConstraint(
                    "CK_BillDisputes_ResolutionCompleteness",
                    "([ResolvedAtUtc] IS NULL AND " +
                    "[ResolvedByUserProfileId] IS NULL AND " +
                    "[Resolution] IS NULL) OR " +
                    "([ResolvedAtUtc] IS NOT NULL AND " +
                    "[ResolvedByUserProfileId] IS NOT NULL AND " +
                    "[Resolution] IS NOT NULL)");
            });

        builder.HasKey(dispute => dispute.Id);

        builder.Property(dispute => dispute.Id)
            .ValueGeneratedNever();

        builder.Property(dispute => dispute.Reason)
            .HasMaxLength(BillDispute.MaxReasonLength)
            .IsRequired();

        builder.Property(dispute => dispute.RaisedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(dispute => dispute.Resolution)
            .HasMaxLength(BillDispute.MaxResolutionLength);

        builder.Property(dispute => dispute.ResolvedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<SharedBill>()
            .WithMany(bill => bill.Disputes)
            .HasForeignKey(dispute => dispute.SharedBillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(dispute => dispute.RaisedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(dispute => dispute.ResolvedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dispute => new
        {
            dispute.SharedBillId,
            dispute.RaisedAtUtc
        });

        builder.HasIndex(dispute => dispute.RaisedByUserProfileId);
        builder.HasIndex(dispute => dispute.ResolvedByUserProfileId);
    }
}
