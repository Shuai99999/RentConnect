using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class TenancyConfiguration
    : IEntityTypeConfiguration<Tenancy>
{
    public void Configure(
        EntityTypeBuilder<Tenancy> builder)
    {
        builder.ToTable(
            "Tenancies",
            table => table.HasCheckConstraint(
                "CK_Tenancies_DateRange",
                "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));

        builder.HasKey(tenancy => tenancy.Id);

        builder.Property(tenancy => tenancy.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(tenancy => tenancy.EndDate)
            .HasColumnType("date");

        builder.Property(tenancy => tenancy.IsCancelled)
            .IsRequired();

        builder.Property(tenancy => tenancy.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.HasOne<RentalUnit>()
            .WithMany()
            .HasForeignKey(tenancy => tenancy.RentalUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tenancy => new
        {
            tenancy.RentalUnitId,
            tenancy.StartDate
        });

        builder.HasIndex(tenancy => tenancy.CreatedAtUtc);

        builder.Navigation(tenancy => tenancy.Members)
            .HasField("_members")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
