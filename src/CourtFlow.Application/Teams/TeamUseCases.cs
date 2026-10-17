using CourtFlow.Domain;

namespace CourtFlow.Application.Teams;

public sealed class TeamUseCases(ITournamentRepository tournaments, IClubRepository clubs, IOrganizationScope organization)
{
    public async Task<IReadOnlyList<TournamentTeamDetails>?> ListAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        var owners = await clubs.GetByTeamsAsync(tournament.Entries.Select(entry => entry.TeamId), cancellationToken);
        var teams = owners
            .SelectMany(club => club.Teams.Select(team => (team, club)))
            .ToDictionary(item => item.team.Id);
        var divisionOrder = Division.OrderForList(tournament.Divisions, division => division.Name, division => division.Gender.ToString())
            .Select((division, index) => (division, index))
            .ToDictionary(item => item.division.Id, item => item.index);

        return tournament.Entries
            .Select(entry => ToDetails(tournament, entry, teams[entry.TeamId].team, teams[entry.TeamId].club))
            .OrderBy(details => divisionOrder[details.DivisionId])
            .ThenBy(details => details.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(details => details.ClubName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TournamentTeamDetails?> AddAsync(Guid tournamentId, AddTournamentTeam command, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        Club.Validate(command.ClubName);
        Team.Validate(command.TeamName);

        var club = await clubs.GetByNameAsync(command.ClubName!, cancellationToken);
        if (club is null)
        {
            club = Club.Create(organization.OrganizationId, command.ClubName);
            clubs.Add(club);
        }

        var team = club.FindTeam(command.TeamName) ?? club.AddTeam(command.TeamName);
        var entry = tournament.EnterTeam(team.Id, command.DivisionId);
        await tournaments.SaveChangesAsync(cancellationToken);
        return ToDetails(tournament, entry, team, club);
    }

    public async Task<TournamentTeamDetails?> UpdateAsync(Guid entryId, UpdateTournamentTeam command, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetByEntryAsync(entryId, cancellationToken);
        if (tournament is null)
            return null;

        // The rename lands on the club, so the archived tournament must stop it before the club is touched.
        tournament.EnsureEditable();
        var entry = tournament.Entries.Single(item => item.Id == entryId);
        var club = (await clubs.GetByTeamsAsync([entry.TeamId], cancellationToken)).Single();
        var team = club.RenameTeam(entry.TeamId, command.TeamName);
        tournament.MoveEntry(entryId, command.DivisionId);
        await tournaments.SaveChangesAsync(cancellationToken);
        return ToDetails(tournament, entry, team, club);
    }

    public async Task<bool> DeleteAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetByEntryAsync(entryId, cancellationToken);
        if (tournament is null)
            return false;

        tournament.RemoveEntry(entryId);
        await tournaments.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ClubDetails>> SearchClubsAsync(string? search, CancellationToken cancellationToken) =>
        (await clubs.SearchAsync(search, cancellationToken))
            .Select(club => new ClubDetails(club.Id, club.Name))
            .ToList();

    public async Task<IReadOnlyList<ClubTeamOption>?> ListClubTeamsAsync(Guid clubId, Guid? tournamentId, CancellationToken cancellationToken)
    {
        var club = await clubs.GetAsync(clubId, cancellationToken);
        if (club is null)
            return null;

        var tournament = tournamentId is { } id ? await tournaments.GetAsync(id, cancellationToken) : null;
        return club.Teams
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Select(team => new ClubTeamOption(team.Id, team.Name, tournament?.EntryFor(team.Id) is not null))
            .ToList();
    }

    private static TournamentTeamDetails ToDetails(Tournament tournament, TournamentTeam entry, Team team, Club club)
    {
        var division = tournament.Divisions.Single(item => item.Id == entry.DivisionId);
        return new TournamentTeamDetails(
            entry.Id,
            tournament.Id,
            team.Id,
            team.Name,
            club.Id,
            club.Name,
            division.Id,
            division.Name,
            division.Gender.ToString());
    }
}
