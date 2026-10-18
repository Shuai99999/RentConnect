using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class TenancyInvitationConfiguration : IEntityTypeConfiguration<TenancyInvitation>
{
    public void Configure(EntityTypeBuilder<TenancyInvitation> builder)
    {
        builder.ToTable("TenancyInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Email).HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(invitation => invitation.JoinedOn).HasColumnType("date").IsRequired();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.TenancyId, invitation.NormalizedEmail });
        builder.HasOne<Tenancy>().WithMany().HasForeignKey(invitation => invitation.TenancyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(invitation => invitation.InvitedByUserProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}
