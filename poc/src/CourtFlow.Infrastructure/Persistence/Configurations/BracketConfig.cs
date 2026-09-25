using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public class BracketConfig : IEntityTypeConfiguration<Bracket>
{
    public void Configure(EntityTypeBuilder<Bracket> b)
    {
        b.ToTable("brackets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.HasMany(x => x.Nodes).WithOne().HasForeignKey(n => n.BracketId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.DisplayName).HasMaxLength(100);
        b.HasIndex(x => x.DivisionId);
        b.HasIndex(x => x.TournamentId);
    }
}

public class BracketNodeConfig : IEntityTypeConfiguration<BracketNode>
{
    public void Configure(EntityTypeBuilder<BracketNode> b)
    {
        b.ToTable("bracket_nodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Branch).HasConversion<int>();
        b.HasIndex(x => new { x.BracketId, x.Branch, x.Round, x.Slot }).IsUnique();
    }
}
