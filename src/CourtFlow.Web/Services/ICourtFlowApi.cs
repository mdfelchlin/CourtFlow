namespace CourtFlow.Web.Services;

public interface ICourtFlowApi
{
    Task<IReadOnlyList<TournamentResponse>> ListTournamentsAsync(CancellationToken cancellationToken = default);
    Task<TournamentResponse?> GetTournamentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TournamentResponse> CreateTournamentAsync(TournamentRequest request, CancellationToken cancellationToken = default);
    Task<TournamentResponse> UpdateTournamentAsync(Guid id, TournamentRequest request, CancellationToken cancellationToken = default);
    Task DeleteTournamentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VenueResponse>> ListVenuesAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<VenueResponse> CreateVenueAsync(Guid tournamentId, VenueRequest request, CancellationToken cancellationToken = default);
    Task<VenueResponse> UpdateVenueAsync(Guid venueId, VenueRequest request, CancellationToken cancellationToken = default);
    Task DeleteVenueAsync(Guid venueId, CancellationToken cancellationToken = default);
}
