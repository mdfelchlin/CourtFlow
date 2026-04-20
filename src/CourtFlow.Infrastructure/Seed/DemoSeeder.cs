using CourtFlow.Application.Brackets;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Identity;
using CourtFlow.Infrastructure.MultiTenancy;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.Seed;

public static class DemoSeeder
{
    public const string DemoOrgSlug = "courtflow-demo";
    public const string DemoAdminEmail = "admin@courtflow.local";
    public const string DemoAdminPassword = "Admin!234";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));
        }

        var org = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == DemoOrgSlug, ct);
        if (org is null)
        {
            org = new Organization("CourtFlow Demo Club", DemoOrgSlug);
            db.Organizations.Add(org);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded demo organization {OrgId}", org.Id);
        }

        var admin = await userManager.FindByEmailAsync(DemoAdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = DemoAdminEmail,
                Email = DemoAdminEmail,
                EmailConfirmed = true,
                DisplayName = "Demo Admin"
            };
            var result = await userManager.CreateAsync(admin, DemoAdminPassword);
            if (!result.Succeeded) throw new InvalidOperationException("Failed to create demo admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        var hasMembership = await db.OrganizationMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.OrganizationId == org.Id && m.UserId == admin.Id, ct);
        if (!hasMembership)
        {
            db.OrganizationMemberships.Add(new OrganizationMembership(org.Id, admin.Id, "Admin"));
            await db.SaveChangesAsync(ct);
        }

        using var tenantScope = HttpTenantContext.BeginScope(org.Id, admin.Id);

        var tournament = await db.Tournaments.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.OrganizationId == org.Id, ct);
        if (tournament is null)
        {
            var start = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            tournament = new Tournament(org.Id, "Spring Classic", start, start, "America/Los_Angeles", 60);
            db.Tournaments.Add(tournament);

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

            for (var i = 0; i < mensTeamNames.Length; i++)
            {
                var pool = mensPools[i / 3];
                var wave = i % 2 == 0 ? Wave.AM : Wave.PM;
                var team = new Team(org.Id, mensDivision.Id, mensTeamNames[i], wave, pool.Id, club: "Demo Club", seedHint: (i % 3) + 1);
                db.Teams.Add(team);
            }
            for (var i = 0; i < womensTeamNames.Length; i++)
            {
                var pool = womensPools[i / 4];
                var wave = i % 2 == 0 ? Wave.AM : Wave.PM;
                var team = new Team(org.Id, womensDivision.Id, womensTeamNames[i], wave, pool.Id, club: "Demo Club", seedHint: (i % 4) + 1);
                db.Teams.Add(team);
            }
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
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Seeded demo tournament {TournamentId}", tournament.Id);
        }
    }
}
