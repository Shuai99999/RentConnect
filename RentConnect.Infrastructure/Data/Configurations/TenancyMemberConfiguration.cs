using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentConnect.Core.Entities;

namespace RentConnect.Infrastructure.Data.Configurations;

public sealed class TenancyMemberConfiguration
    : IEntityTypeConfiguration<TenancyMember>
{
    public void Configure(
        EntityTypeBuilder<TenancyMember> builder)
    {
        builder.ToTable(
            "TenancyMembers",
            table => table.HasCheckConstraint(
                "CK_TenancyMembers_DateRange",
                "[LeftOn] IS NULL OR [LeftOn] >= [JoinedOn]"));

        builder.HasKey(member => member.Id);

        builder.Property(member => member.JoinedOn)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(member => member.LeftOn)
            .HasColumnType("date");

        builder.HasOne<Tenancy>()
            .WithMany(tenancy => tenancy.Members)
            .HasForeignKey(member => member.TenancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(member => member.UserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(member => new
        {
            member.TenancyId,
            member.UserProfileId,
            member.JoinedOn
        })
        .IsUnique();

        builder.HasIndex(member => member.UserProfileId);
    }
}