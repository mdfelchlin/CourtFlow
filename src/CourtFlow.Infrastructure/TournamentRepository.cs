using CourtFlow.Application;
using CourtFlow.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        WithChildren(db.Tournaments)
            .FirstOrDefaultAsync(tournament => tournament.Id == id, cancellationToken);

    public Task<Tournament?> GetByVenueAsync(Guid venueId, CancellationToken cancellationToken) =>
        WithChildren(db.Tournaments)
            .FirstOrDefaultAsync(
                tournament => tournament.Venues.Any(venue => venue.Id == venueId),
                cancellationToken);

    public Task<Tournament?> GetByDivisionAsync(Guid divisionId, CancellationToken cancellationToken) =>
        WithChildren(db.Tournaments)
            .FirstOrDefaultAsync(
                tournament => tournament.Divisions.Any(division => division.Id == divisionId),
                cancellationToken);

    public Task<Tournament?> GetByEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
        WithChildren(db.Tournaments)
            .FirstOrDefaultAsync(
                tournament => tournament.Entries.Any(entry => entry.Id == entryId),
                cancellationToken);

    private static IQueryable<Tournament> WithChildren(IQueryable<Tournament> tournaments) =>
        tournaments
            .Include(tournament => tournament.Venues)
            .ThenInclude(venue => venue.Courts)
            .Include(tournament => tournament.Divisions)
            .Include(tournament => tournament.Entries)
            .AsSplitQuery();

    public async Task AddAsync(Tournament tournament, CancellationToken cancellationToken) =>
        await db.Tournaments.AddAsync(tournament, cancellationToken);

    public void Remove(Tournament tournament) => db.Tournaments.Remove(tournament);

    /// <summary>Saves every tracked change, tournaments and clubs alike, in one transaction.</summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainRuleException(SaveConflict.Message);
        }
    }
}
