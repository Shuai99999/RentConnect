using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class RentalUnitConfiguration
    : IEntityTypeConfiguration<RentalUnit>
{
    public void Configure(
        EntityTypeBuilder<RentalUnit> builder)
    {
        builder.ToTable("RentalUnits");

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Name)
            .HasMaxLength(RentalUnit.MaxNameLength)
            .IsRequired();

        builder.Property(unit => unit.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(unit => unit.IsActive)
            .IsRequired();

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(unit => unit.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(unit => new
        {
            unit.PropertyId,
            unit.Name
        })
        .IsUnique();
    }
}