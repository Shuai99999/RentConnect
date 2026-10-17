using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class SharedBillConfiguration
    : IEntityTypeConfiguration<SharedBill>
{
    public void Configure(EntityTypeBuilder<SharedBill> builder)
    {
        builder.ToTable(
            "SharedBills",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_SharedBills_TotalAmount",
                    "[TotalAmount] > 0");
                table.HasCheckConstraint(
                    "CK_SharedBills_BillingPeriod",
                    "[PeriodEnd] >= [PeriodStart]");
                table.HasCheckConstraint(
                    "CK_SharedBills_DueDate",
                    "[DueDate] >= [PeriodEnd]");
                table.HasCheckConstraint(
                    "CK_SharedBills_UpdateTime",
                    "[UpdatedAtUtc] >= [CreatedAtUtc]");
            });

        builder.HasKey(bill => bill.Id);

        builder.Property(bill => bill.Id)
            .ValueGeneratedNever();

        builder.Property(bill => bill.Title)
            .HasMaxLength(SharedBill.MaxTitleLength)
            .IsRequired();

        builder.Property(bill => bill.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(bill => bill.SplitMethod)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(bill => bill.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.OwnsOne(
            bill => bill.Total,
            money =>
            {
                money.Property(value => value.Amount)
                    .HasColumnName("TotalAmount")
                    .HasPrecision(18, 4)
                    .IsRequired();

                money.Property(value => value.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsFixedLength()
                    .IsUnicode(false)
                    .IsRequired();
            });

        builder.Property(bill => bill.PeriodStart)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(bill => bill.PeriodEnd)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(bill => bill.DueDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(bill => bill.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(bill => bill.UpdatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.HasOne<Tenancy>()
            .WithMany()
            .HasForeignKey(bill => bill.TenancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(bill => bill.CreatedByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(bill => bill.PaidByUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(bill => new
        {
            bill.TenancyId,
            bill.Status,
            bill.CreatedAtUtc
        });

        builder.HasIndex(bill => new
        {
            bill.TenancyId,
            bill.DueDate
        });

        builder.HasIndex(bill => bill.CreatedByUserProfileId);
        builder.HasIndex(bill => bill.PaidByUserProfileId);

        builder.Navigation(bill => bill.Shares)
            .HasField("_shares")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(bill => bill.Disputes)
            .HasField("_disputes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
