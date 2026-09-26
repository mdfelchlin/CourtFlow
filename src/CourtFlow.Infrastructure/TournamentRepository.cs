using CourtFlow.Application;
using CourtFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure;

public sealed class TournamentRepository(CourtFlowDbContext db) : ITournamentRepository
{
    public async Task<IReadOnlyList<TournamentDetails>> ListAsync(CancellationToken cancellationToken) =>
        await db.Tournaments
            .AsNoTracking()
            .OrderBy(tournament => tournament.StartDate)
            .ThenBy(tournament => tournament.Name)
            .Select(tournament => new TournamentDetails(
                tournament.Id,
                tournament.Name,
                tournament.Description,
                tournament.StartDate,
                tournament.EndDate,
                tournament.TimeZoneId,
                tournament.DefaultMatchDurationMinutes))
            .ToListAsync(cancellationToken);

    public Task<Tournament?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Tournaments
            .Include(tournament => tournament.Venues)
            .ThenInclude(venue => venue.Courts)
            .FirstOrDefaultAsync(tournament => tournament.Id == id, cancellationToken);

    public Task<Tournament?> GetByVenueAsync(Guid venueId, CancellationToken cancellationToken) =>
        db.Tournaments
            .Include(tournament => tournament.Venues)
            .ThenInclude(venue => venue.Courts)
            .FirstOrDefaultAsync(
                tournament => tournament.Venues.Any(venue => venue.Id == venueId),
                cancellationToken);

    public async Task AddAsync(Tournament tournament, CancellationToken cancellationToken) =>
        await db.Tournaments.AddAsync(tournament, cancellationToken);

    public void Remove(Tournament tournament) => db.Tournaments.Remove(tournament);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
