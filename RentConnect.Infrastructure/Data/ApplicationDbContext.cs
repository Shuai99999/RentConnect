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

    public DbSet<Property> Properties =>
        Set<Property>();

    public DbSet<PropertyOwner> PropertyOwners =>
        Set<PropertyOwner>();

    public DbSet<RentalUnit> RentalUnits =>
        Set<RentalUnit>();

    public DbSet<Tenancy> Tenancies =>
        Set<Tenancy>();

    public DbSet<TenancyMember> TenancyMembers =>
        Set<TenancyMember>();

    public DbSet<TenancyInvitation> TenancyInvitations =>
        Set<TenancyInvitation>();

    public DbSet<MaintenanceRequest> MaintenanceRequests =>
        Set<MaintenanceRequest>();

    public DbSet<MaintenanceStatusChange>
        MaintenanceStatusChanges =>
        Set<MaintenanceStatusChange>();

    public DbSet<MaintenanceMessage> MaintenanceMessages =>
        Set<MaintenanceMessage>();

    public DbSet<MaintenanceAttachment>
        MaintenanceAttachments =>
        Set<MaintenanceAttachment>();

    public DbSet<UserNotification> UserNotifications =>
        Set<UserNotification>();

    public DbSet<SharedBill> SharedBills =>
        Set<SharedBill>();

    public DbSet<BillShare> BillShares =>
        Set<BillShare>();

    public DbSet<BillDispute> BillDisputes =>
        Set<BillDispute>();

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

        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}
