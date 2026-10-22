using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class PropertyConfiguration
    : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(property => property.Id);

        builder.Property(property => property.Name)
            .IsRequired();

        builder.Property(property => property.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.HasIndex(property => property.CreatedAtUtc);

        var address = builder.ComplexProperty(
            property => property.Address);

        address.Property(value => value.Line1)
            .HasColumnName("AddressLine1")
            .IsRequired();

        address.Property(value => value.Line2)
            .HasColumnName("AddressLine2");

        address.Property(value => value.City)
            .HasColumnName("AddressCity")
            .IsRequired();

        address.Property(value => value.Region)
            .HasColumnName("AddressRegion");

        address.Property(value => value.PostalCode)
            .HasColumnName("AddressPostalCode");

        address.Property(value => value.CountryCode)
            .HasColumnName("AddressCountryCode")
            .HasMaxLength(2)
            .IsFixedLength()
            .IsRequired();

        var location = builder.ComplexProperty(
            property => property.Location);

        location.Property(value => value.Latitude)
            .HasColumnName("Latitude");

        location.Property(value => value.Longitude)
            .HasColumnName("Longitude");

        builder.Navigation(property => property.Owners)
            .HasField("_owners")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
