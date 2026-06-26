using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Venues;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Tests;

public class VenueCourtSyncTests
{
    [Fact]
    public async Task SyncAsync_CreatesNumberedCourts()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), "America/Los_Angeles");
        var venue = new Venue(orgId, tournament.Id, "Gym");
        db.Tournaments.Add(tournament);
        db.Venues.Add(venue);
        await db.SaveChangesAsync();

        var count = await VenueCourtSync.SyncAsync(db, orgId, venue.Id, 3, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(3, count);
        var names = await db.Courts.Where(c => c.VenueId == venue.Id).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
        Assert.Equal(new[] { "1", "2", "3" }, names);
    }

    [Fact]
    public async Task SyncAsync_AppendsCourtsWhenIncreasing()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), "America/Los_Angeles");
        var venue = new Venue(orgId, tournament.Id, "Gym");
        db.Tournaments.Add(tournament);
        db.Venues.Add(venue);
        db.Courts.Add(new Court(orgId, venue.Id, "1"));
        db.Courts.Add(new Court(orgId, venue.Id, "2"));
        await db.SaveChangesAsync();

        await VenueCourtSync.SyncAsync(db, orgId, venue.Id, 4, CancellationToken.None);
        await db.SaveChangesAsync();

        var names = await db.Courts.Where(c => c.VenueId == venue.Id).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
        Assert.Equal(new[] { "1", "2", "3", "4" }, names);
    }

    [Fact]
    public async Task SyncAsync_RemovesHighestNumberedCourtsWhenDecreasing()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), "America/Los_Angeles");
        var venue = new Venue(orgId, tournament.Id, "Gym");
        db.Tournaments.Add(tournament);
        db.Venues.Add(venue);
        foreach (var n in new[] { "1", "2", "3", "4" })
            db.Courts.Add(new Court(orgId, venue.Id, n));
        await db.SaveChangesAsync();

        await VenueCourtSync.SyncAsync(db, orgId, venue.Id, 2, CancellationToken.None);
        await db.SaveChangesAsync();

        var names = await db.Courts.Where(c => c.VenueId == venue.Id).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
        Assert.Equal(new[] { "1", "2" }, names);
    }

    [Fact]
    public async Task SyncAsync_BlocksRemovalWhenCourtHasScheduledMatch()
    {
        await using var db = BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolOnly, 16);
        var venue = new Venue(orgId, tournament.Id, "Gym");
        var court1 = new Court(orgId, venue.Id, "1");
        var court2 = new Court(orgId, venue.Id, "2");
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Venues.Add(venue);
        db.Courts.AddRange(court1, court2);
        db.Matches.Add(new Match(
            orgId,
            tournament.Id,
            division.Id,
            Wave.AM,
            DateTime.UtcNow.AddHours(1),
            60,
            courtId: court2.Id));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            VenueCourtSync.SyncAsync(db, orgId, venue.Id, 1, CancellationToken.None));

        Assert.Contains("court '2'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TestAppDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }
}
