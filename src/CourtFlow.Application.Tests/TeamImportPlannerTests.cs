using CourtFlow.Application.Teams.Import;
using CourtFlow.Domain;

namespace CourtFlow.Application.Tests;

public class TeamImportPlannerTests
{
    private static readonly Guid Organization = Guid.NewGuid();

    [Theory]
    [InlineData("", "", "", "X", TeamImportPlanner.ClubRequired)]
    [InlineData("Club", "", "", "X", TeamImportPlanner.TeamRequired)]
    [InlineData("Club", "Team", "", "X", TeamImportPlanner.DivisionRequired)]
    [InlineData("Club", "Team", "14U Open", "X", TeamImportPlanner.GenderInvalid)]
    public void RowChecksReportTheFirstFailureInOrder(string club, string team, string division, string gender, string message)
    {
        var (tournament, _, _) = Setup();

        var row = Single(Plan(tournament, [], Row(2, club, team, division, gender)));

        Assert.Equal(TeamImportOutcome.Rejected, row.Outcome);
        Assert.Equal(message, row.Message);
    }

    [Fact]
    public void AMissingDivisionNamesTheRowSpellingAndResolvedGender()
    {
        var (tournament, _, _) = Setup();

        var row = Single(Plan(tournament, [], Row(2, "Club", "Team", "Men's Division II", "m")));

        Assert.Equal("Add the division \"Men's Division II (Male)\" to the tournament before importing.", row.Message);
    }

    [Fact]
    public void DivisionNameMatchesIgnoringCaseButGenderMustMatchToo()
    {
        var (tournament, _, _) = Setup();

        var plan = Plan(tournament, [], Row(2, "Club", "Team", " 14u open ", "F"), Row(3, "Club", "Other", "14U Open", "M"));

        Assert.Equal(TeamImportOutcome.AddNewClubAndTeam, plan.Preview.Rows[0].Outcome);
        Assert.Equal(TeamImportOutcome.Rejected, plan.Preview.Rows[1].Outcome);
    }

    [Fact]
    public void EveryDuplicateRowIsRejectedAndListsEveryLine()
    {
        var (tournament, _, _) = Setup();

        var plan = Plan(
            tournament,
            [],
            Row(4, "Ohio State University", "Ohio St U", "14U Open", "F"),
            Row(5, "Other", "Other", "14U Open", "F"),
            Row(9, " ohio state university ", "OHIO ST U", "14U Open", "F"),
            Row(12, "Ohio State University", "ohio st u", "14U Open", "F"));

        var duplicates = plan.Preview.Rows.Where(row => row.Line != 5).ToList();
        Assert.All(duplicates, row => Assert.Equal("This team is on more than one row (lines 4, 9, and 12).", row.Message));
        Assert.Equal(TeamImportOutcome.AddNewClubAndTeam, plan.Preview.Rows.Single(row => row.Line == 5).Outcome);
        Assert.Single(plan.Accepted);
    }

    [Fact]
    public void TwoDuplicateRowsSayAnd()
    {
        var (tournament, _, _) = Setup();

        var plan = Plan(tournament, [], Row(4, "A", "B", "14U Open", "F"), Row(9, "a", "b", "14U Open", "F"));

        Assert.Equal("This team is on more than one row (lines 4 and 9).", plan.Preview.Rows[0].Message);
    }

    [Fact]
    public void OutcomesCoverNewExistingMovedAndUnchanged()
    {
        var (tournament, open, gold) = Setup();
        var club = Club.Create(Organization, "Ohio State University");
        var unentered = club.AddTeam("Ohio St B");
        var stays = club.AddTeam("Ohio St A");
        var moves = club.AddTeam("Ohio St C");
        tournament.EnterTeam(stays.Id, open.Id);
        tournament.EnterTeam(moves.Id, gold.Id);

        var plan = Plan(
            tournament,
            [club],
            Row(2, "New Club", "New Team", "14U Open", "F"),
            Row(3, "ohio state university", "Brand New", "14U Open", "F"),
            Row(4, "Ohio State University", unentered.Name, "14U Open", "F"),
            Row(5, "Ohio State University", "OHIO ST A", "14U Open", "F"),
            Row(6, "Ohio State University", moves.Name, "14U Open", "F"));

        Assert.Equal(
            [TeamImportOutcome.AddNewClubAndTeam, TeamImportOutcome.AddNewTeam, TeamImportOutcome.AddExistingTeam, TeamImportOutcome.NoChange, TeamImportOutcome.Move],
            plan.Preview.Rows.Select(row => row.Outcome).ToArray());
        Assert.Equal("14U Gold (Female)", plan.Preview.Rows[4].FromDivision);
        Assert.Equal(new TeamImportSummary(Rows: 5, NewClubs: 1, NewTeams: 2, ToAdd: 3, ToMove: 1, Unchanged: 1, Rejected: 0), plan.Preview.Summary);
        Assert.Equal(5, plan.Accepted.Count);
    }

    [Fact]
    public void ANewClubNamedWithDifferentCaseCountsOnce()
    {
        var (tournament, _, _) = Setup();

        var plan = Plan(tournament, [], Row(2, "Liberty University", "Liberty A", "14U Open", "F"), Row(3, "LIBERTY UNIVERSITY", "Liberty B", "14U Open", "F"));

        Assert.Equal(1, plan.Preview.Summary.NewClubs);
        Assert.Equal(2, plan.Preview.Summary.NewTeams);
        Assert.All(plan.Preview.Rows, row => Assert.Equal(TeamImportOutcome.AddNewClubAndTeam, row.Outcome));
    }

    [Fact]
    public void AReaderRejectionIsKeptAndCounted()
    {
        var (tournament, _, _) = Setup();

        var plan = Plan(tournament, [], new TeamCsvRow(2, "A", "B", "14U Open", "F", TeamCsvReader.ExtraValues));

        var row = Single(plan);
        Assert.Equal(TeamCsvReader.ExtraValues, row.Message);
        Assert.Equal(1, plan.Preview.Summary.Rejected);
        Assert.Empty(plan.Accepted);
    }

    [Fact]
    public void EntriesMissingFromTheFileAreNotTouched()
    {
        var (tournament, open, _) = Setup();
        var club = Club.Create(Organization, "Kept Club");
        var kept = club.AddTeam("Kept");
        tournament.EnterTeam(kept.Id, open.Id);

        var plan = Plan(tournament, [club], Row(2, "Other", "Other", "14U Open", "F"));

        Assert.DoesNotContain(plan.Accepted, row => row.TeamName == "Kept");
        Assert.Single(tournament.Entries);
    }

    private static (Tournament Tournament, Division Open, Division Gold) Setup()
    {
        var tournament = Tournament.Create(Organization, "City Cup", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "America/Phoenix", 60);
        var open = tournament.AddDivision("14U Open", "Female");
        var gold = tournament.AddDivision("14U Gold", "Female");
        return (tournament, open, gold);
    }

    private static TeamCsvRow Row(int line, string club, string team, string division, string gender) =>
        new(line, club.Trim(), team.Trim(), division.Trim(), gender.Trim(), null);

    private static TeamImportPlan Plan(Tournament tournament, IReadOnlyList<Club> clubs, params TeamCsvRow[] rows) =>
        TeamImportPlanner.Plan(rows, tournament, clubs);

    private static TeamImportRow Single(TeamImportPlan plan) => Assert.Single(plan.Preview.Rows);
}