using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentConnect.Core.Entities;

namespace RentConnect.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserProfile> UserProfiles =>
        Set<UserProfile>();

    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");

            entity.HasKey(profile => profile.Id);

            entity.Property(profile => profile.DisplayName)
                .IsRequired();
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(user => user.UserProfile)
                .WithOne()
                .HasForeignKey<ApplicationUser>(
                    user => user.UserProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(user => user.UserProfileId)
                .IsUnique();
        });
    }
}