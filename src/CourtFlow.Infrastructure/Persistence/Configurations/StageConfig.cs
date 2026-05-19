using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public sealed class StageConfig : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> b)
    {
        b.ToTable("stages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.StageType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.SeedingSource).HasMaxLength(64);
        b.HasIndex(x => new { x.DivisionId, x.StageNumber }).IsUnique();
    }
}

public sealed class TournamentTeamConfig : IEntityTypeConfiguration<TournamentTeam>
{
    public void Configure(EntityTypeBuilder<TournamentTeam> b)
    {
        b.ToTable("tournament_teams");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntryStatus).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CheckedInStatus).HasMaxLength(32);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.WithdrawalReason).HasMaxLength(500);
        b.HasIndex(x => new { x.TournamentId, x.DivisionId });
        b.HasIndex(x => new { x.TournamentId, x.TeamId });
    }
}

public sealed class DivisionRankingConfig : IEntityTypeConfiguration<DivisionRanking>
{
    public void Configure(EntityTypeBuilder<DivisionRanking> b)
    {
        b.ToTable("division_rankings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.DivisionId, x.Rank }).IsUnique();
        b.HasIndex(x => new { x.DivisionId, x.TournamentTeamId }).IsUnique();
    }
}

public sealed class PoolEntryConfig : IEntityTypeConfiguration<PoolEntry>
{
    public void Configure(EntityTypeBuilder<PoolEntry> b)
    {
        b.ToTable("pool_entries");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.PoolId, x.SeedInPool }).IsUnique();
        b.HasIndex(x => new { x.PoolId, x.TournamentTeamId }).IsUnique();
    }
}

public sealed class AdvancementRuleConfig : IEntityTypeConfiguration<AdvancementRule>
{
    public void Configure(EntityTypeBuilder<AdvancementRule> b)
    {
        b.ToTable("advancement_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.OrderBy).HasMaxLength(32);
        b.HasIndex(x => x.SourceStageId);
        b.HasIndex(x => x.TargetBracketId);
    }
}

public sealed class BracketSeedSlotConfig : IEntityTypeConfiguration<BracketSeedSlot>
{
    public void Configure(EntityTypeBuilder<BracketSeedSlot> b)
    {
        b.ToTable("bracket_seed_slots");
        b.HasKey(x => x.Id);
        b.Property(x => x.Label).HasMaxLength(100);
        b.Property(x => x.ExpectedSourceType).HasMaxLength(32);
        b.HasIndex(x => new { x.BracketId, x.SeedPosition }).IsUnique();
    }
}

public sealed class PoolCourtAllocationConfig : IEntityTypeConfiguration<PoolCourtAllocation>
{
    public void Configure(EntityTypeBuilder<PoolCourtAllocation> b)
    {
        b.ToTable("pool_court_allocations");
        b.HasKey(x => x.Id);
        b.Property(x => x.AllocationType).HasMaxLength(16).IsRequired();
        b.HasIndex(x => new { x.PoolId, x.CourtId }).IsUnique();
        b.HasIndex(x => new { x.PoolId, x.PriorityOrder });
    }
}

public sealed class BracketSeedSlotOverrideConfig : IEntityTypeConfiguration<BracketSeedSlotOverride>
{
    public void Configure(EntityTypeBuilder<BracketSeedSlotOverride> b)
    {
        b.ToTable("bracket_seed_slot_overrides");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorUserId).HasMaxLength(128);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => x.BracketSeedSlotId);
        b.HasIndex(x => x.AtUtc);
    }
}
