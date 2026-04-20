using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("organizations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasMany(x => x.Tournaments).WithOne().HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Memberships).WithOne().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrganizationMembershipConfig : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> b)
    {
        b.ToTable("organization_memberships");
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
    }
}
