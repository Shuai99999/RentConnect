using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class RentalUnitImageConfiguration
    : IEntityTypeConfiguration<RentalUnitImage>
{
    public void Configure(EntityTypeBuilder<RentalUnitImage> builder)
    {
        builder.ToTable("RentalUnitImages");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).ValueGeneratedNever();
        builder.Property(image => image.ContentType)
            .IsRequired()
            .HasMaxLength(30);
        builder.Property(image => image.Content)
            .IsRequired()
            .HasMaxLength(RentalUnitImage.MaxBytes);
        builder.Property(image => image.UploadedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.HasIndex(image => new
        {
            image.RentalUnitId,
            image.UploadedAtUtc
        });
        builder.HasOne<RentalUnit>()
            .WithMany()
            .HasForeignKey(image => image.RentalUnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
