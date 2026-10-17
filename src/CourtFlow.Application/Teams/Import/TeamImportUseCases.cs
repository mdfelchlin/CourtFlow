using CourtFlow.Domain;

namespace CourtFlow.Application.Teams.Import;

/// <summary>Either the import was written, or nothing was written and the fresh preview says why.</summary>
public sealed record TeamImportAttempt(TeamImportResult? Imported, TeamImportPreview? Stale);

public sealed class TeamImportUseCases(ITournamentRepository tournaments, IClubRepository clubs, IOrganizationScope organization)
{
    public async Task<TeamImportPreview?> PreviewAsync(Guid tournamentId, byte[] file, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        tournament.EnsureEditable();
        var (preview, _, _) = await PlanAsync(tournament, file, cancellationToken);
        return preview;
    }

    public async Task<TeamImportAttempt?> ImportAsync(Guid tournamentId, byte[] file, CancellationToken cancellationToken)
    {
        var tournament = await tournaments.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        tournament.EnsureEditable();
        var (preview, accepted, loaded) = await PlanAsync(tournament, file, cancellationToken);
        if (preview.FileError is not null || preview.Summary.Rejected > 0)
            return new TeamImportAttempt(null, preview with { Stale = true });

        var clubsByKey = loaded.ToDictionary(club => club.NormalizedName);
        foreach (var row in accepted)
        {
            var key = Club.Normalize(row.ClubName);
            if (!clubsByKey.TryGetValue(key, out var club))
            {
                club = Club.Create(organization.OrganizationId, row.ClubName);
                clubs.Add(club);
                clubsByKey[key] = club;
            }

            var team = club.FindTeam(row.TeamName) ?? club.AddTeam(row.TeamName);
            if (tournament.EntryFor(team.Id) is not { } entry)
                tournament.EnterTeam(team.Id, row.DivisionId);
            else if (entry.DivisionId != row.DivisionId)
                tournament.MoveEntry(entry.Id, row.DivisionId);
        }

        await tournaments.SaveChangesAsync(cancellationToken);
        return new TeamImportAttempt(new TeamImportResult(preview.Summary.Rows), null);
    }

    private async Task<(TeamImportPreview Preview, IReadOnlyList<PlannedTeamRow> Accepted, IReadOnlyList<Club> Clubs)> PlanAsync(
        Tournament tournament,
        byte[] file,
        CancellationToken cancellationToken)
    {
        var csv = TeamCsvReader.Read(file);
        if (csv.Error is not null)
            return (TeamImportPlanner.FileFailed(csv.Error), [], []);

        var named = await clubs.GetByNamesAsync(
            csv.Rows.Where(row => row.Club.Length > 0).Select(row => row.Club),
            cancellationToken);
        var plan = TeamImportPlanner.Plan(csv.Rows, tournament, named);
        return (plan.Preview, plan.Accepted, named);
    }
}
