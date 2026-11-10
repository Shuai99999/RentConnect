using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class PropertyImageConfiguration
    : IEntityTypeConfiguration<PropertyImage>
{
    public void Configure(
        EntityTypeBuilder<PropertyImage> builder)
    {
        builder.ToTable("PropertyImages");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .ValueGeneratedNever();

        builder.Property(image => image.ContentType)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(image => image.Content)
            .IsRequired()
            .HasMaxLength(PropertyImage.MaxBytes);

        builder.Property(image => image.UploadedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.HasIndex(image => new
        {
            image.PropertyId,
            image.UploadedAtUtc
        });

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(image => image.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}