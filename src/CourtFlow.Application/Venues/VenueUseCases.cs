using CourtFlow.Domain;

namespace CourtFlow.Application.Venues;

public sealed class VenueUseCases(ITournamentRepository repository)
{
    public async Task<IReadOnlyList<VenueDetails>?> ListAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(tournamentId, cancellationToken);
        return tournament is null ? null : tournament.Venues.Select(ToDetails).ToList();
    }

    public async Task<VenueDetails?> CreateAsync(Guid tournamentId, SaveVenue command, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        var venue = tournament.AddVenue(command.Name, command.Address, RequireCourtCount(command.CourtCount));
        await repository.SaveChangesAsync(cancellationToken);
        return ToDetails(venue);
    }

    public async Task<VenueDetails?> UpdateAsync(Guid venueId, SaveVenue command, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetByVenueAsync(venueId, cancellationToken);
        if (tournament is null)
            return null;

        var venue = tournament.UpdateVenue(venueId, command.Name, command.Address, RequireCourtCount(command.CourtCount));
        await repository.SaveChangesAsync(cancellationToken);
        return ToDetails(venue);
    }

    public async Task<bool> DeleteAsync(Guid venueId, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetByVenueAsync(venueId, cancellationToken);
        if (tournament is null)
            return false;

        tournament.RemoveVenue(venueId);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static int RequireCourtCount(int? courtCount)
    {
        if (courtCount is null)
            throw new DomainRuleException("Court count is required.");

        return courtCount.Value;
    }

    internal static VenueDetails ToDetails(Domain.Venue venue) =>
        new(
            venue.Id,
            venue.TournamentId,
            venue.Name,
            venue.Address,
            venue.Courts.Count,
            venue.Courts
                .OrderBy(court => court.Number)
                .Select(court => new CourtDetails(court.Id, court.Number))
                .ToList());
}