using CourtFlow.Domain;

namespace CourtFlow.Application.Rankings;

public sealed class RankingUseCases(ITournamentRepository tournaments, IClubRepository clubs)
{
    public async Task<IReadOnlyList<FormatDivisionDetails>?> ListAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        return Division.OrderForList(tournament.Divisions, division => division.Name, division => division.Gender.ToString())
            .Select(division =>
            {
                var teamCount = tournament.Entries.Count(entry => entry.DivisionId == division.Id);
                var ranked = tournament.IsRanked(division.Id);
                var newTeams = ranked ? tournament.NewTeamCount(division.Id) : 0;
                return new FormatDivisionDetails(
                    division.Id,
                    tournament.Id,
                    division.Name,
                    division.Gender.ToString(),
                    teamCount,
                    ranked,
                    newTeams,
                    RankingWording.Status(teamCount, ranked, newTeams));
            })
            .ToList();
    }

    public async Task<DivisionRankingDetails?> GetAsync(Guid divisionId, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetByDivisionAsync(divisionId, cancellationToken);
        if (tournament is null)
            return null;

        return await ToDetailsAsync(tournament, divisionId, cancellationToken);
    }

    public async Task<DivisionRankingDetails?> SaveAsync(Guid divisionId, SaveRanking command, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetByDivisionAsync(divisionId, cancellationToken);
        if (tournament is null)
            return null;

        tournament.SaveRanking(divisionId, command.EntryIds ?? []);
        await tournaments.SaveChangesAsync(cancellationToken);
        return await ToDetailsAsync(tournament, divisionId, cancellationToken);
    }

    private async Task<DivisionRankingDetails> ToDetailsAsync(Tournament tournament, Guid divisionId, CancellationToken cancellationToken)
    {
        var division = tournament.Divisions.Single(item => item.Id == divisionId);
        var entries = tournament.Entries.Where(entry => entry.DivisionId == divisionId).ToList();
        var teams = entries.Count == 0
            ? []
            : (await clubs.GetByTeamsAsync(entries.Select(entry => entry.TeamId), cancellationToken))
                .SelectMany(club => club.Teams.Select(team => (team, club)))
                .ToDictionary(item => item.team.Id);

        var ranked = tournament.IsRanked(divisionId);
        IReadOnlyList<RankingTeamDetails> rows;
        if (ranked)
        {
            var byEntry = entries.ToDictionary(entry => entry.Id);
            rows = tournament.RankingOf(divisionId)
                .Select(place =>
                {
                    var entry = byEntry[place.EntryId];
                    var (team, club) = teams[entry.TeamId];
                    return new RankingTeamDetails(entry.Id, club.Name, team.Name, place.Rank, place.IsNew);
                })
                .ToList();
        }
        else
        {
            rows = entries
                .Select(entry => (entry, team: teams[entry.TeamId].team, club: teams[entry.TeamId].club))
                .OrderBy(item => item.team.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.club.Name, StringComparer.OrdinalIgnoreCase)
                .Select((item, index) => new RankingTeamDetails(item.entry.Id, item.club.Name, item.team.Name, index + 1, false))
                .ToList();
        }

        return new DivisionRankingDetails(
            division.Id,
            tournament.Id,
            tournament.Name,
            tournament.Status,
            division.Name,
            division.Gender.ToString(),
            ranked,
            rows);
    }
}
