using System.Globalization;

using CourtFlow.Domain;

namespace CourtFlow.Application.Teams.Import;

/// <summary>A row that passed every check, with what applying it needs.</summary>
public sealed record PlannedTeamRow(string ClubName, string TeamName, Guid DivisionId);

public sealed record TeamImportPlan(TeamImportPreview Preview, IReadOnlyList<PlannedTeamRow> Accepted);

/// <summary>Checks each row and decides its outcome against the tournament and the clubs the file names. Writes nothing.</summary>
public static class TeamImportPlanner
{
    public const string ClubRequired = "Club is required.";
    public const string TeamRequired = "Team is required.";
    public const string DivisionRequired = "Division is required.";
    public const string GenderInvalid = "Gender must be M, F, Male, or Female.";

    public static string MissingDivision(string division, DivisionGender gender) =>
        $"Add the division \"{Division.Label(division, gender.ToString())}\" to the tournament before importing.";

    public static string DuplicateRows(IReadOnlyList<int> lines) =>
        $"This team is on more than one row (lines {TeamCsvReader.JoinWithAnd(lines.Select(line => line.ToString(CultureInfo.InvariantCulture)).ToList())}).";

    public static TeamImportPreview FileFailed(string error) =>
        new(error, [], new TeamImportSummary(0, 0, 0, 0, 0, 0, 0), Stale: false);

    public static TeamImportPlan Plan(IReadOnlyList<TeamCsvRow> rows, Tournament tournament, IReadOnlyList<Club> clubs)
    {
        var duplicateLines = rows
            .Where(row => row.Error is null && row.Club.Length > 0 && row.Team.Length > 0)
            .GroupBy(row => (Club.Normalize(row.Club), Club.Normalize(row.Team)))
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(row => (row.Line, lines: group.Select(item => item.Line).ToList())))
            .ToDictionary(item => item.Line, item => item.lines);
        var clubsByKey = clubs.ToDictionary(club => club.NormalizedName);
        var newClubKeys = new HashSet<string>();

        var previewRows = new List<TeamImportRow>();
        var accepted = new List<PlannedTeamRow>();
        foreach (var row in rows)
        {
            TeamImportRow Rejected(string message) => ToRow(row, TeamImportOutcome.Rejected, null, message);

            if (row.Error is not null)
            {
                previewRows.Add(Rejected(row.Error));
                continue;
            }

            if (Check(row, tournament, duplicateLines, out var division) is { } message)
            {
                previewRows.Add(Rejected(message));
                continue;
            }

            var clubKey = Club.Normalize(row.Club);
            var club = clubsByKey.GetValueOrDefault(clubKey);
            var team = club?.FindTeam(row.Team);
            var entry = team is null ? null : tournament.EntryFor(team.Id);

            TeamImportRow planned;
            if (club is null)
            {
                newClubKeys.Add(clubKey);
                planned = ToRow(row, TeamImportOutcome.AddNewClubAndTeam, null, null);
            }
            else if (team is null)
                planned = ToRow(row, TeamImportOutcome.AddNewTeam, null, null);
            else if (entry is null)
                planned = ToRow(row, TeamImportOutcome.AddExistingTeam, null, null);
            else if (entry.DivisionId == division!.Id)
                planned = ToRow(row, TeamImportOutcome.NoChange, null, null);
            else
            {
                var from = tournament.Divisions.Single(item => item.Id == entry.DivisionId);
                planned = ToRow(row, TeamImportOutcome.Move, Division.Label(from.Name, from.Gender.ToString()), null);
            }

            previewRows.Add(planned);
            accepted.Add(new PlannedTeamRow(row.Club, row.Team, division!.Id));
        }

        int Count(params TeamImportOutcome[] outcomes) => previewRows.Count(row => outcomes.Contains(row.Outcome));
        var summary = new TeamImportSummary(
            Rows: previewRows.Count,
            NewClubs: newClubKeys.Count,
            NewTeams: Count(TeamImportOutcome.AddNewClubAndTeam, TeamImportOutcome.AddNewTeam),
            ToAdd: Count(TeamImportOutcome.AddNewClubAndTeam, TeamImportOutcome.AddNewTeam, TeamImportOutcome.AddExistingTeam),
            ToMove: Count(TeamImportOutcome.Move),
            Unchanged: Count(TeamImportOutcome.NoChange),
            Rejected: Count(TeamImportOutcome.Rejected));
        return new TeamImportPlan(new TeamImportPreview(null, previewRows, summary, Stale: false), accepted);
    }

    /// <summary>The spec's row checks, in order. Returns the first failure's message, or null when the row passes.</summary>
    private static string? Check(
        TeamCsvRow row,
        Tournament tournament,
        Dictionary<int, List<int>> duplicateLines,
        out Division? division)
    {
        division = null;
        if (row.Club.Length == 0)
            return ClubRequired;
        if (row.Team.Length == 0)
            return TeamRequired;
        if (row.Division.Length == 0)
            return DivisionRequired;
        if (Division.ParseImportGender(row.Gender) is not { } gender)
            return GenderInvalid;

        var key = Club.Normalize(row.Division);
        division = tournament.Divisions.FirstOrDefault(item => item.Gender == gender && Club.Normalize(item.Name) == key);
        if (division is null)
            return MissingDivision(row.Division, gender);
        if (duplicateLines.TryGetValue(row.Line, out var lines))
            return DuplicateRows(lines);

        return null;
    }

    private static TeamImportRow ToRow(TeamCsvRow row, TeamImportOutcome outcome, string? fromDivision, string? message) =>
        new(row.Line, row.Club, row.Team, row.Division, row.Gender, outcome, fromDivision, message);
}