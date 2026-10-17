using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class BillShareConfiguration
    : IEntityTypeConfiguration<BillShare>
{
    public void Configure(EntityTypeBuilder<BillShare> builder)
    {
        builder.ToTable(
            "BillShares",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_BillShares_Amount",
                    "[Amount] >= 0");
                table.HasCheckConstraint(
                    "CK_BillShares_PaymentSequence",
                    "[PaymentConfirmedAtUtc] IS NULL OR " +
                    "([PaymentDeclaredAtUtc] IS NOT NULL AND " +
                    "[PaymentConfirmedAtUtc] >= [PaymentDeclaredAtUtc])");
            });

        builder.HasKey(share => share.Id);

        builder.Property(share => share.Id)
            .ValueGeneratedNever();

        builder.OwnsOne(
            share => share.Amount,
            money =>
            {
                money.Property(value => value.Amount)
                    .HasColumnName("Amount")
                    .HasPrecision(18, 4)
                    .IsRequired();

                money.Property(value => value.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsFixedLength()
                    .IsUnicode(false)
                    .IsRequired();
            });

        builder.Property(share => share.PaymentDeclaredAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(share => share.PaymentConfirmedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<SharedBill>()
            .WithMany(bill => bill.Shares)
            .HasForeignKey(share => share.SharedBillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(share => share.UserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(share => new
        {
            share.SharedBillId,
            share.UserProfileId
        }).IsUnique();

        builder.HasIndex(share => share.UserProfileId);
    }
}
