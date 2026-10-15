using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class PropertyOwnerConfiguration
    : IEntityTypeConfiguration<PropertyOwner>
{
    public void Configure(
        EntityTypeBuilder<PropertyOwner> builder)
    {
        builder.ToTable("PropertyOwners");

        builder.HasKey(owner => new
        {
            owner.PropertyId,
            owner.UserProfileId
        });

        builder.HasOne<Property>()
            .WithMany(property => property.Owners)
            .HasForeignKey(owner => owner.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(owner => owner.UserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(owner => owner.UserProfileId);
    }
}