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
    Task<IReadOnlyList<DivisionResponse>> ListDivisionsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<DivisionResponse> CreateDivisionAsync(Guid tournamentId, DivisionRequest request, CancellationToken cancellationToken = default);
    Task<DivisionResponse> UpdateDivisionAsync(Guid divisionId, DivisionRequest request, CancellationToken cancellationToken = default);
    Task DeleteDivisionAsync(Guid divisionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentTeamResponse>> ListTournamentTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TournamentTeamResponse> AddTournamentTeamAsync(Guid tournamentId, TournamentTeamRequest request, CancellationToken cancellationToken = default);
    Task<TournamentTeamResponse> UpdateTournamentTeamAsync(Guid entryId, TournamentTeamUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteTournamentTeamAsync(Guid entryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClubResponse>> SearchClubsAsync(string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClubTeamOptionResponse>> ListClubTeamsAsync(Guid clubId, Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TeamImportPreviewResponse> PreviewTeamImportAsync(Guid tournamentId, string fileName, byte[] content, CancellationToken cancellationToken = default);
    Task<TeamImportResponse> ImportTeamsAsync(Guid tournamentId, string fileName, byte[] content, CancellationToken cancellationToken = default);
}
