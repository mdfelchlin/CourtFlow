using CourtFlow.Domain;

namespace CourtFlow.Application;

public interface ITournamentRepository
{
    Task<IReadOnlyList<TournamentDetails>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Tournament>> ListDueForArchiveAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<Tournament?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Tournament?> GetByVenueAsync(Guid venueId, CancellationToken cancellationToken);
    Task<Tournament?> GetByDivisionAsync(Guid divisionId, CancellationToken cancellationToken);
    Task<Tournament?> GetByEntryAsync(Guid entryId, CancellationToken cancellationToken);
    Task AddAsync(Tournament tournament, CancellationToken cancellationToken);
    void Remove(Tournament tournament);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
