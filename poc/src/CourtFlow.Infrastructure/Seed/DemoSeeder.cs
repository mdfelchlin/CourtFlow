using CourtFlow.Application.Brackets;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.MultiTenancy;
using CourtFlow.Infrastructure.Options;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourtFlow.Infrastructure.Seed;

public static class DemoSeeder
{
    public const string DemoOrgSlug = "courtflow-demo";

    /// <summary>
    /// Config key (e.g. user-secrets) for the Clerk <c>sub</c> of the local developer.
    /// When present, the seeder attaches an admin <see cref="OrganizationMembership"/>
    /// for this <c>sub</c> on the demo org so the developer's real login lands inside
    /// the seeded tenant. When absent, no membership is attached — the demo data exists
    /// but is invisible to any real user until they're invited.
    /// </summary>
    public const string DevClerkUserIdConfigKey = "Dev:ClerkUserId";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>();
        if (!databaseOptions.Value.SeedDemoData)
            return;

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var devClerkUserId = config[DevClerkUserIdConfigKey]?.Trim();

        var org = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == DemoOrgSlug, ct);
        if (org is null)
        {
            org = new Organization("CourtFlow Demo Club", DemoOrgSlug);
            db.Organizations.Add(org);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded demo organization {OrgId}", org.Id);
        }

        if (!string.IsNullOrWhiteSpace(devClerkUserId))
        {
            var hasMembership = await db.OrganizationMemberships.IgnoreQueryFilters()
                .AnyAsync(m => m.OrganizationId == org.Id && m.ExternalUserId == devClerkUserId, ct);
            if (!hasMembership)
            {
                db.OrganizationMemberships.Add(new OrganizationMembership(org.Id, devClerkUserId, "Admin"));
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Attached demo-org admin membership for Dev:ClerkUserId={Sub}", devClerkUserId);
            }
        }
        else
        {
            logger.LogDebug(
                "Dev:ClerkUserId not configured — demo tournament will exist but no membership is attached. " +
                "Set 'Dev:ClerkUserId' in user-secrets to your Clerk sub to see Spring Classic on login.");
        }

        using var tenantScope = HttpTenantContext.BeginScope(org.Id, devClerkUserId);

        var tournament = await db.Tournaments.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.OrganizationId == org.Id, ct);
        if (tournament is null)
        {
            var start = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            tournament = new Tournament(org.Id, "Spring Classic", start, start, "America/Los_Angeles", 60);
            tournament.SetDescription("Demo tournament seeded on startup.");
            tournament.SetContactInfo("https://courtflow.example/demo", "demo@courtflow.example");
            tournament.SetSessionMode(TournamentSessionMode.MultiWave);
            db.Tournaments.Add(tournament);
            db.ScheduleVersions.Add(new ScheduleVersion(org.Id, tournament.Id));

            var amWave = new WaveEntity(org.Id, tournament.Id, "AM", 0);
            amWave.SetTimeWindow("08:00", null);
            var pmWave = new WaveEntity(org.Id, tournament.Id, "PM", 1);
            pmWave.SetTimeWindow("15:00", null);
            db.Waves.AddRange(amWave, pmWave);

            var venue = new Venue(org.Id, tournament.Id, "Main Gym");
            db.Venues.Add(venue);

            var amCourts = new[] { "Court 1 AM", "Court 2 AM", "Court 3 AM" }
                .Select(n => new Court(org.Id, venue.Id, n, new[] { "AM" })).ToList();
            var pmCourts = new[] { "Court 4 PM", "Court 5 PM", "Court 6 PM" }
                .Select(n => new Court(org.Id, venue.Id, n, new[] { "PM" })).ToList();
            foreach (var c in amCourts.Concat(pmCourts)) db.Courts.Add(c);

            var mensDivision = new Division(org.Id, tournament.Id, "Men's Open", DivisionFormat.PoolPlusBracket, 16);
            var womensDivision = new Division(org.Id, tournament.Id, "Women's Open", DivisionFormat.PoolPlusBracket, 16);
            db.Divisions.AddRange(mensDivision, womensDivision);

            await DefaultDivisionStages.EnsureProvisionedAsync(db, mensDivision, ct);
            await DefaultDivisionStages.EnsureProvisionedAsync(db, womensDivision, ct);

            var mensPools = new[]
            {
                new Pool(org.Id, mensDivision.Id, "Men's Pool A"),
                new Pool(org.Id, mensDivision.Id, "Men's Pool B"),
                new Pool(org.Id, mensDivision.Id, "Men's Pool C")
            };
            var womensPools = new[]
            {
                new Pool(org.Id, womensDivision.Id, "Women's Pool A"),
                new Pool(org.Id, womensDivision.Id, "Women's Pool B"),
                new Pool(org.Id, womensDivision.Id, "Women's Pool C")
            };
            db.Pools.AddRange(mensPools);
            db.Pools.AddRange(womensPools);

            var mensTeamNames = new[]
            {
                "Riptide", "Spikers", "Sand Storm",
                "Net Ninjas", "Volleyhawks", "Ace Masters",
                "Kill Shot", "Setters United", "Beach Bombers"
            };
            var womensTeamNames = new[]
            {
                "High Tide", "Boardwalk", "Seagulls", "Tridents",
                "Cyclones", "Wave Breakers", "Sand Dollars", "Coastal Crush",
                "Tidal Force", "Lightning", "Breakers", "Pacific Power"
            };

            var mensTeams = new List<Team>();
            for (var i = 0; i < mensTeamNames.Length; i++)
            {
                var pool = mensPools[i / 3];
                var wave = i % 2 == 0 ? Wave.AM : Wave.PM;
                var rank = i + 1;
                var team = new Team(org.Id, mensDivision.Id, mensTeamNames[i], wave, pool.Id, club: "Demo Club", seedHint: rank);
                db.Teams.Add(team);
                mensTeams.Add(team);
            }
            var womensTeams = new List<Team>();
            for (var i = 0; i < womensTeamNames.Length; i++)
            {
                var pool = womensPools[i / 4];
                var wave = i % 2 == 0 ? Wave.AM : Wave.PM;
                var rank = i + 1;
                var team = new Team(org.Id, womensDivision.Id, womensTeamNames[i], wave, pool.Id, club: "Demo Club", seedHint: rank);
                db.Teams.Add(team);
                womensTeams.Add(team);
            }
            await db.SaveChangesAsync(ct);

            await SeedDivisionRankingsAsync(db, org.Id, tournament.Id, mensDivision, mensTeams, ct);
            await SeedDivisionRankingsAsync(db, org.Id, tournament.Id, womensDivision, womensTeams, ct);
            await db.SaveChangesAsync(ct);

            var scheduler = scope.ServiceProvider.GetRequiredService<IScheduleGenerator>();
            await scheduler.GeneratePoolPlayAsync(tournament.Id, ct);

            var bracketBuilder = scope.ServiceProvider.GetRequiredService<IBracketBuilder>();
            await bracketBuilder.BuildAsync(tournament.Id, womensDivision.Id,
                new BracketBuildOptions(BracketType.SingleElimination, 8, false), ct);

            var demoMatches = await db.Matches.IgnoreQueryFilters()
                .Where(m => m.TournamentId == tournament.Id && m.PoolId != null)
                .OrderBy(m => m.ScheduledStartUtc)
                .Take(4)
                .ToListAsync(ct);
            foreach (var m in demoMatches)
            {
                var s1 = m.ReportSet(1, 25, 18);
                var s2 = m.ReportSet(2, 25, 22);
                db.MatchSets.Add(s1);
                db.MatchSets.Add(s2);
                m.Finalize(m.HomeTeamId);
            }
            tournament.Publish();
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Seeded demo tournament {TournamentId}", tournament.Id);
        }
    }

    private static async Task SeedDivisionRankingsAsync(
        AppDbContext db,
        Guid organizationId,
        Guid tournamentId,
        Division division,
        IReadOnlyList<Team> teams,
        CancellationToken ct)
    {
        for (var i = 0; i < teams.Count; i++)
        {
            var rank = i + 1;
            var team = teams[i];
            var tournamentTeam = await db.TournamentTeams.FirstOrDefaultAsync(
                tt => tt.TournamentId == tournamentId && tt.TeamId == team.Id, ct);
            if (tournamentTeam is null)
            {
                tournamentTeam = new TournamentTeam(organizationId, tournamentId, division.Id, team.Id);
                db.TournamentTeams.Add(tournamentTeam);
            }

            var existingRanking = await db.DivisionRankings.FirstOrDefaultAsync(
                r => r.DivisionId == division.Id && r.TournamentTeamId == tournamentTeam.Id, ct);
            if (existingRanking is null)
            {
                db.DivisionRankings.Add(new DivisionRanking(
                    organizationId, division.Id, tournamentTeam.Id, rank, source: "manual"));
            }
            else
            {
                existingRanking.SetRank(rank, "manual", isManualOverride: false);
            }
        }
    }
}
