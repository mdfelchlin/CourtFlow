using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Imports;
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
            .Select(i => ImportRankedTeamsHandler.GetSnakePoolIndex(i, 3))
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
        var teams = await db.Teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.SeedHint).ToListAsync();
        Assert.Equal(2, pools.Count);
        Assert.Equal(6, teams.Count);

        var expectedBySeed = new[] { "Pool A", "Pool B", "Pool B", "Pool A", "Pool A", "Pool B" };
        for (var i = 0; i < teams.Count; i++)
        {
            var assignedPool = pools.Single(p => p.Id == teams[i].PoolId).Name;
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
        var sizes = ImportRankedTeamsHandler.BuildPoolSizes(teamCount);
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

    private static TestAppDbContext BuildDb()
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
}
