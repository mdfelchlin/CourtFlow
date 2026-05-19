using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Divisions;
using CourtFlow.Application.Features.Imports;
using CourtFlow.Application.Features.Pools;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Infrastructure.StageMaterialization;
using CourtFlow.Application.Features.Teams;
using CourtFlow.Application.Seeding;
using CourtFlow.Application.Standings;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Tests;

public class ImportFeaturesTests
{
    [Fact]
    public void SnakeAssignment_UsesSerpentinePattern()
    {
        var actual = Enumerable.Range(0, 6)
            .Select(i => PoolSeeding.GetSnakePoolIndex(i, 3))
            .ToArray();

        Assert.Equal(new[] { 0, 1, 2, 2, 1, 0 }, actual);
    }

    [Fact]
    public async Task RankedImport_AssignsTeamsToSnakePools()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Men's Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await db.SaveChangesAsync();

        var handler = new ImportRankedTeamsHandler(db);
        var result = await handler.Handle(
            new ImportRankedTeamsCommand(
                tournament.Id,
                Strategy: RankedSeedStrategy.Snake,
                Rows: new[]
                {
                    new RankedTeamImportRow(2, "Men's Open", 1, "Team 1", null, Wave.AM),
                    new RankedTeamImportRow(3, "Men's Open", 2, "Team 2", null, Wave.AM),
                    new RankedTeamImportRow(4, "Men's Open", 3, "Team 3", null, Wave.AM),
                    new RankedTeamImportRow(5, "Men's Open", 4, "Team 4", null, Wave.AM),
                    new RankedTeamImportRow(6, "Men's Open", 5, "Team 5", null, Wave.AM),
                    new RankedTeamImportRow(7, "Men's Open", 6, "Team 6", null, Wave.AM),
                }),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.CreatedPools);
        Assert.Equal(6, result.CreatedTeams);

        var pools = await db.Pools.Where(p => p.DivisionId == division.Id).OrderBy(p => p.Name).ToListAsync();
        var teams = await db.Teams.Where(t => t.DivisionId == division.Id).ToListAsync();
        Assert.Equal(2, pools.Count);
        Assert.Equal(6, teams.Count);
        Assert.Equal(6, await db.TournamentTeams.CountAsync(tt => tt.DivisionId == division.Id));
        Assert.Equal(6, await db.DivisionRankings.CountAsync(r => r.DivisionId == division.Id));

        var ranks = await (
            from r in db.DivisionRankings
            join tt in db.TournamentTeams on r.TournamentTeamId equals tt.Id
            where r.DivisionId == division.Id
            orderby r.Rank
            select new { tt.TeamId, r.Rank, r.Source }
        ).ToListAsync();
        Assert.All(ranks, r => Assert.Equal("import", r.Source));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, ranks.Select(r => r.Rank));

        var teamsByRank = ranks.Select(r => teams.Single(t => t.Id == r.TeamId)).ToList();
        var expectedBySeed = new[] { "Pool A", "Pool B", "Pool B", "Pool A", "Pool A", "Pool B" };
        for (var i = 0; i < teamsByRank.Count; i++)
        {
            var assignedPool = pools.Single(p => p.Id == teamsByRank[i].PoolId).Name;
            Assert.Equal(expectedBySeed[i], assignedPool);
        }
    }

    [Fact]
    public async Task RankedImport_ReturnsErrors_ForDuplicateRanks()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Women's Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await db.SaveChangesAsync();

        var handler = new ImportRankedTeamsHandler(db);
        var result = await handler.Handle(
            new ImportRankedTeamsCommand(
                tournament.Id,
                Strategy: RankedSeedStrategy.Snake,
                Rows: new[]
                {
                    new RankedTeamImportRow(2, "Women's Open", 1, "Team A", null, Wave.AM),
                    new RankedTeamImportRow(3, "Women's Open", 1, "Team B", null, Wave.AM)
                }),
            CancellationToken.None);

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.RowNumber == 2);
        Assert.Contains(result.Errors, e => e.RowNumber == 3);
    }

    [Theory]
    [MemberData(nameof(PoolSizeCases))]
    public void BuildPoolSizes_UsesExpectedLayout(int teamCount, int expectedPools, int[] expectedSizes)
    {
        var sizes = PoolSeeding.BuildPoolSizes(teamCount);
        Assert.Equal(expectedPools, sizes.Count);
        Assert.Equal(expectedSizes, sizes);
    }

    public static IEnumerable<object[]> PoolSizeCases =>
        new[]
        {
            new object[] { 5, 1, new[] { 5 } },
            new object[] { 10, 3, new[] { 4, 3, 3 } },
            new object[] { 11, 3, new[] { 4, 4, 3 } },
            new object[] { 14, 4, new[] { 4, 4, 3, 3 } }
        };

    [Fact]
    public async Task RankedImport_ReturnsError_WhenDivisionHasLessThan3Teams()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        var handler = new ImportRankedTeamsHandler(db);
        var result = await handler.Handle(
            new ImportRankedTeamsCommand(
                tournament.Id,
                Strategy: RankedSeedStrategy.Snake,
                Rows: new[]
                {
                    new RankedTeamImportRow(2, "Boys 12", 1, "Team A", null, Wave.AM),
                    new RankedTeamImportRow(3, "Boys 12", 2, "Team B", null, Wave.AM),
                }),
            CancellationToken.None);

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("minimum is 3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PredefinedImport_CreatesDivision_WhenMissing()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        var handler = new ImportPredefinedPoolsHandler(db);
        var result = await handler.Handle(
            new ImportPredefinedPoolsCommand(
                tournament.Id,
                Rows: new[]
                {
                    new PredefinedPoolImportRow(2, "Unknown", "Pool A", "Team A", null, Wave.AM, 1),
                    new PredefinedPoolImportRow(3, "Unknown", "Pool A", "Team B", null, Wave.AM, 2),
                    new PredefinedPoolImportRow(4, "Unknown", "Pool A", "Team C", null, Wave.AM, 3)
                }),
            CancellationToken.None);

        Assert.Empty(result.Errors);
        var division = await db.Divisions.FirstOrDefaultAsync(d => d.TournamentId == tournament.Id && d.Name == "Unknown");
        Assert.NotNull(division);
        Assert.Equal(1, await db.Pools.CountAsync());
        Assert.Equal(3, await db.Teams.CountAsync());
        Assert.Equal(3, await db.TournamentTeams.CountAsync(tt => tt.DivisionId == division.Id));
        Assert.Equal(3, await db.DivisionRankings.CountAsync(r => r.DivisionId == division.Id));
        Assert.All(
            await db.DivisionRankings.Where(r => r.DivisionId == division.Id).ToListAsync(),
            r => Assert.Equal("import_seed_hint", r.Source));
    }

    [Fact]
    public async Task PredefinedImport_ReturnsError_WhenPoolHasFewerThan3Teams()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        var handler = new ImportPredefinedPoolsHandler(db);
        var result = await handler.Handle(
            new ImportPredefinedPoolsCommand(
                tournament.Id,
                Rows: new[]
                {
                    new PredefinedPoolImportRow(2, "Men's Open", "Pool A", "Team A", null, Wave.AM, 1),
                    new PredefinedPoolImportRow(3, "Men's Open", "Pool A", "Team B", null, Wave.AM, 2)
                }),
            CancellationToken.None);

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.RowNumber == 2);
        Assert.Contains(result.Errors, e => e.RowNumber == 3);
        Assert.Contains(result.Errors, e => e.Message.Contains("at least 3 teams", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyDivisionSeeding_SnakeAssignsPoolsFromSeedOrder()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        for (var i = 1; i <= 6; i++)
        {
            var team = new Team(orgId, division.Id, $"Team {i}", Wave.AM, poolId: null, club: null, seedHint: null);
            team.SetSeedHint(i);
            db.Teams.Add(team);
        }
        await db.SaveChangesAsync();

        var handler = new ApplyDivisionSeedingHandler(db);
        var r = await handler.Handle(new ApplyDivisionSeedingCommand(division.Id, RankedSeedStrategy.Snake), CancellationToken.None);

        Assert.True(r.Ok);
        var pools = await db.Pools.Where(p => p.DivisionId == division.Id).OrderBy(p => p.Name).ToListAsync();
#pragma warning disable CS0618
        var teams = await db.Teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.SeedHint).ToListAsync();
#pragma warning restore CS0618
        Assert.Equal(2, pools.Count);
        var expectedBySeed = new[] { "Pool A", "Pool B", "Pool B", "Pool A", "Pool A", "Pool B" };
        for (var i = 0; i < teams.Count; i++)
        {
            var poolName = pools.Single(p => p.Id == teams[i].PoolId).Name;
            Assert.Equal(expectedBySeed[i], poolName);
        }
    }

    [Fact]
    public async Task ReorderDivisionTeams_WritesDivisionRankings()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        var a = new Team(orgId, division.Id, "A", Wave.AM, null, null, 1);
        var b = new Team(orgId, division.Id, "B", Wave.AM, null, null, 2);
        db.Teams.AddRange(a, b);
        await db.SaveChangesAsync();

        var handler = new ReorderDivisionTeamsHandler(db);
        var result = await handler.Handle(new ReorderDivisionTeamsCommand(division.Id, new[] { b.Id, a.Id }), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("B", result[0].Name);
        Assert.Equal(1, result[0].SeedHint);
        Assert.Equal("A", result[1].Name);
        Assert.Equal(2, result[1].SeedHint);

        var rankings = await (
            from r in db.DivisionRankings
            join tt in db.TournamentTeams on r.TournamentTeamId equals tt.Id
            where r.DivisionId == division.Id
            orderby r.Rank
            select new { tt.TeamId, r.Rank, r.Source, r.IsManualOverride }
        ).ToListAsync();
        Assert.Equal(2, rankings.Count);
        Assert.Equal(b.Id, rankings[0].TeamId);
        Assert.Equal(1, rankings[0].Rank);
        Assert.Equal("manual", rankings[0].Source);
        Assert.True(rankings[0].IsManualOverride);
        Assert.Equal(a.Id, rankings[1].TeamId);
        Assert.Equal(2, rankings[1].Rank);
    }

    [Fact]
    public async Task ListTeams_OrdersByDivisionRanking_NotLegacySeedHint()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        var first = new Team(orgId, division.Id, "First", Wave.AM, null, null, seedHint: 99);
        var second = new Team(orgId, division.Id, "Second", Wave.AM, null, null, seedHint: 1);
        db.Teams.AddRange(first, second);
        await db.SaveChangesAsync();

        var ttFirst = new TournamentTeam(orgId, tournament.Id, division.Id, first.Id);
        var ttSecond = new TournamentTeam(orgId, tournament.Id, division.Id, second.Id);
        db.TournamentTeams.AddRange(ttFirst, ttSecond);
        db.DivisionRankings.Add(new DivisionRanking(orgId, division.Id, ttFirst.Id, rank: 1, source: "manual"));
        db.DivisionRankings.Add(new DivisionRanking(orgId, division.Id, ttSecond.Id, rank: 2, source: "manual"));
        await db.SaveChangesAsync();

        var handler = new ListTeamsHandler(db);
        var listed = await handler.Handle(new ListTeamsQuery(null, division.Id, null), CancellationToken.None);

        Assert.Equal(new[] { "First", "Second" }, listed.Select(t => t.Name));
        Assert.Equal(new int?[] { 1, 2 }, listed.Select(t => t.SeedHint).ToArray());
    }

    [Fact]
    public async Task DefaultDivisionStages_ProvisionCreatesPoolThenBracketStages()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await db.SaveChangesAsync();

        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Stages.CountAsync(s => s.DivisionId == division.Id));
        var ordered = await db.Stages.Where(s => s.DivisionId == division.Id).OrderBy(s => s.StageNumber).ToListAsync();
        Assert.Equal(StageType.Pool, ordered[0].StageType);
        Assert.Equal(StageType.Bracket, ordered[1].StageType);
    }

    [Fact]
    public async Task DeletePool_UnassignsTeams()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var pool = new Pool(orgId, division.Id, "Pool A");
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Pools.Add(pool);
        var team = new Team(orgId, division.Id, "T1", Wave.AM, pool.Id, null, 1);
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var handler = new DeletePoolHandler(db);
        var ok = await handler.Handle(new DeletePoolCommand(pool.Id), CancellationToken.None);
        Assert.True(ok);

        var t = await db.Teams.FirstAsync(x => x.Id == team.Id);
        Assert.Null(t.PoolId);
    }

    [Fact]
    public async Task SnakeIntoPools_WritesPoolEntriesAndSeededStage()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        var teams = new List<Team>();
        for (var i = 1; i <= 6; i++)
        {
            var team = new Team(orgId, division.Id, $"Team {i}", Wave.AM);
            teams.Add(team);
            db.Teams.Add(team);
            var tt = new TournamentTeam(orgId, tournament.Id, division.Id, team.Id);
            db.TournamentTeams.Add(tt);
            db.DivisionRankings.Add(new DivisionRanking(orgId, division.Id, tt.Id, i, "test"));
        }
        await db.SaveChangesAsync();

        var service = new StageMaterializationService(db, new FakeStandingsCalculator());
        await service.SnakeIntoPoolsAsync(division.Id);

        var entries = await db.PoolEntries.ToListAsync();
        Assert.Equal(6, entries.Count);

        var poolStage = await db.Stages.FirstAsync(s => s.DivisionId == division.Id && s.StageType == StageType.Pool);
        Assert.Equal(StageStatus.Seeded, poolStage.Status);

        var matches = await db.Matches.Where(m => m.DivisionId == division.Id && m.PoolId != null).ToListAsync();
        Assert.True(matches.Count > 0);
        Assert.All(matches, m => Assert.Equal(poolStage.Id, m.StageId));
    }

    [Fact]
    public async Task GenerateBrackets_CreatesSeedSlotsAndNodes()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();
        var poolStage = await db.Stages.FirstAsync(s => s.DivisionId == division.Id && s.StageType == StageType.Pool);

        var bracket = new Bracket(orgId, tournament.Id, division.Id, BracketType.SingleElimination, 4, displayName: "Gold");
        db.Brackets.Add(bracket);
        db.AdvancementRules.Add(new AdvancementRule(orgId, poolStage.Id, 1, bracket.Id, 1));
        await db.SaveChangesAsync();

        var standings = new FakeStandingsCalculator();
        var service = new StageMaterializationService(db, standings);
        await service.GenerateBracketsAsync(division.Id);

        var slots = await db.BracketSeedSlots.Where(s => s.BracketId == bracket.Id).ToListAsync();
        Assert.Equal(4, slots.Count);
        var nodes = await db.BracketNodes.Where(n => n.BracketId == bracket.Id).ToListAsync();
        Assert.True(nodes.Count > 0);
    }

    [Fact]
    public async Task ResolveBracketSlots_WritesResolvedTeams()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var pool = new Pool(orgId, division.Id, "Pool A", 0, 4);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Pools.Add(pool);
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();
        var poolStage = await db.Stages.FirstAsync(s => s.DivisionId == division.Id && s.StageType == StageType.Pool);

        var team = new Team(orgId, division.Id, "Winner", Wave.AM, pool.Id);
        db.Teams.Add(team);
        var tt = new TournamentTeam(orgId, tournament.Id, division.Id, team.Id);
        db.TournamentTeams.Add(tt);
        var entry = new PoolEntry(orgId, pool.Id, tt.Id, 1);
        entry.SetFinishPosition(1);
        db.PoolEntries.Add(entry);

        var bracket = new Bracket(orgId, tournament.Id, division.Id, BracketType.SingleElimination, 2, displayName: "Gold");
        db.Brackets.Add(bracket);
        var rule = new AdvancementRule(orgId, poolStage.Id, 1, bracket.Id, 1, pool.Id);
        db.AdvancementRules.Add(rule);
        await db.SaveChangesAsync();

        var slot1 = new BracketSeedSlot(orgId, bracket.Id, 1, "advancement_rule");
        slot1.LinkAdvancementRule(rule.Id);
        var slot2 = new BracketSeedSlot(orgId, bracket.Id, 2, "bye");
        db.BracketSeedSlots.AddRange(slot1, slot2);

        var node = new BracketNode(orgId, bracket.Id, BracketBranch.Winners, 1, 0);
        db.BracketNodes.Add(node);
        await db.SaveChangesAsync();

        var standings = new FakeStandingsCalculator();
        var service = new StageMaterializationService(db, standings);
        await service.ResolveBracketSlotsAsync(division.Id);

        var resolved = await db.BracketSeedSlots.FirstAsync(s => s.SeedPosition == 1);
        Assert.Equal(tt.Id, resolved.ResolvedTournamentTeamId);
    }

    internal static TestAppDbContext BuildDb()
    {
        var opts = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TestAppDbContext(opts);
    }
}

internal sealed class TestAppDbContext : DbContext, IAppDbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Pool> Pools => Set<Pool>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<Bracket> Brackets => Set<Bracket>();
    public DbSet<BracketNode> BracketNodes => Set<BracketNode>();
    public DbSet<SchedulingTimeSlot> SchedulingTimeSlots => Set<SchedulingTimeSlot>();
    public DbSet<ScheduleVersion> ScheduleVersions => Set<ScheduleVersion>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<DivisionRanking> DivisionRankings => Set<DivisionRanking>();
    public DbSet<PoolEntry> PoolEntries => Set<PoolEntry>();
    public DbSet<AdvancementRule> AdvancementRules => Set<AdvancementRule>();
    public DbSet<BracketSeedSlot> BracketSeedSlots => Set<BracketSeedSlot>();
    public DbSet<PoolCourtAllocation> PoolCourtAllocations => Set<PoolCourtAllocation>();
    public DbSet<BracketSeedSlotOverride> BracketSeedSlotOverrides => Set<BracketSeedSlotOverride>();
    public DbSet<MatchAssignment> MatchAssignments => Set<MatchAssignment>();
    public DbSet<MatchOfficials> MatchOfficials => Set<MatchOfficials>();
    public DbSet<RepairJob> RepairJobs => Set<RepairJob>();
    public DbSet<RepairAction> RepairActions => Set<RepairAction>();
    public DbSet<CourtOutage> CourtOutages => Set<CourtOutage>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<TournamentTemplate> TournamentTemplates => Set<TournamentTemplate>();
    public DbSet<WaveEntity> Waves => Set<WaveEntity>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);
}

internal sealed class FakeStandingsCalculator : IStandingsCalculator
{
    public Task<DivisionStandingsResult> ForDivisionAsync(Guid divisionId, CancellationToken ct = default)
        => Task.FromResult(new DivisionStandingsResult(
            DivisionTiebreakerPreset.WinsH2HSetsPoints.ToString(),
            Array.Empty<PoolStandings>()));
}
