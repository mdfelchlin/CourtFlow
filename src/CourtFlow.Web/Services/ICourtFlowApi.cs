using CourtFlow.Domain;

namespace CourtFlow.Web.Services;

public interface ICourtFlowApi
{
    Task<IReadOnlyList<TournamentResponse>> ListTournamentsAsync(CancellationToken cancellationToken = default);
    Task<TournamentResponse?> GetTournamentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TournamentResponse> CreateTournamentAsync(TournamentRequest request, CancellationToken cancellationToken = default);
    Task<TournamentResponse> UpdateTournamentAsync(Guid id, TournamentRequest request, CancellationToken cancellationToken = default);
    Task DeleteTournamentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TournamentResponse> PublishTournamentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TournamentResponse> UnpublishTournamentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TournamentResponse> ArchiveTournamentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TournamentResponse> RestoreTournamentAsync(Guid id, CancellationToken cancellationToken = default);
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
    Task<IReadOnlyList<FormatDivisionResponse>?> ListFormatAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<DivisionRankingResponse?> GetRankingAsync(Guid divisionId, CancellationToken cancellationToken = default);
    Task<DivisionRankingResponse> SaveRankingAsync(Guid divisionId, IReadOnlyList<Guid> entryIds, CancellationToken cancellationToken = default);

    Task<AccessRequestResultResponse> SubmitAccessRequestAsync(AccessRequestForm form, CancellationToken cancellationToken = default);

    Task<OrganizationOverviewResponse> GetOrganizationAsync(CancellationToken cancellationToken = default);
    Task InviteMemberAsync(string email, OrganizationRole role, CancellationToken cancellationToken = default);
    Task ResendInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task CancelInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task ChangeMemberRoleAsync(Guid membershipId, OrganizationRole role, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid membershipId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccessRequestResponse>> ListAccessRequestsAsync(CancellationToken cancellationToken = default);
    Task ApproveAccessRequestAsync(Guid id, ApproveAccessRequestRequest request, CancellationToken cancellationToken = default);
    Task DeclineAccessRequestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperatorOrganizationResponse>> ListOrganizationsAsync(CancellationToken cancellationToken = default);
    Task InviteOwnerAsync(Guid organizationId, string email, CancellationToken cancellationToken = default);
    Task DeactivateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task ReactivateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}