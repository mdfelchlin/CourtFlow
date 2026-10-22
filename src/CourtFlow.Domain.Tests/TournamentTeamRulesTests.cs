using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class TournamentTeamRulesTests
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 3);

    [Fact]
    public void ATeamIsEnteredOncePerTournament()
    {
        var tournament = Tournament();
        var division = tournament.AddDivision("Men's Division I", "Male");
        var teamId = Guid.NewGuid();

        var entry = tournament.EnterTeam(teamId, division.Id);
        Assert.Equal(division.Id, entry.DivisionId);
        Assert.Same(entry, tournament.EntryFor(teamId));

        var again = Assert.Throws<DomainRuleException>(() => tournament.EnterTeam(teamId, division.Id));
        Assert.Equal(Domain.Tournament.TeamAlreadyEntered, again.Message);
    }

    [Fact]
    public void EnteringOrMovingNeedsADivisionOfThisTournament()
    {
        var tournament = Tournament();
        var own = tournament.AddDivision("Men's Division I", "Male");
        var foreign = Tournament().AddDivision("Men's Division I", "Male");

        Assert.Equal(Division.Required, Assert.Throws<DomainRuleException>(() => tournament.EnterTeam(Guid.NewGuid(), null)).Message);
        Assert.Equal(Division.Required, Assert.Throws<DomainRuleException>(() => tournament.EnterTeam(Guid.NewGuid(), foreign.Id)).Message);

        var entry = tournament.EnterTeam(Guid.NewGuid(), own.Id);
        Assert.Equal(Division.Required, Assert.Throws<DomainRuleException>(() => tournament.MoveEntry(entry.Id, foreign.Id)).Message);
        Assert.Equal(own.Id, entry.DivisionId);
    }

    [Fact]
    public void MovingChangesTheDivisionAndKeepsTheEntry()
    {
        var tournament = Tournament();
        var first = tournament.AddDivision("Men's Division I", "Male");
        var second = tournament.AddDivision("Men's Division IAA", "Male");
        var entry = tournament.EnterTeam(Guid.NewGuid(), first.Id);

        var moved = tournament.MoveEntry(entry.Id, second.Id);

        Assert.Same(entry, moved);
        Assert.Equal(second.Id, entry.DivisionId);
    }

    [Fact]
    public void RemovingAnEntryLeavesTheOtherEntriesAndDivisions()
    {
        var tournament = Tournament();
        var division = tournament.AddDivision("Men's Division I", "Male");
        var removed = tournament.EnterTeam(Guid.NewGuid(), division.Id);
        var kept = tournament.EnterTeam(Guid.NewGuid(), division.Id);

        tournament.RemoveEntry(removed.Id);

        Assert.Equal([kept.Id], tournament.Entries.Select(entry => entry.Id).ToArray());
        Assert.Single(tournament.Divisions);
        Assert.Equal(Domain.Tournament.EntryNotFound, Assert.Throws<DomainRuleException>(() => tournament.RemoveEntry(removed.Id)).Message);
    }

    [Fact]
    public void ADivisionWithTeamsCannotBeRemoved()
    {
        var tournament = Tournament();
        var busy = tournament.AddDivision("Men's Division I", "Male");
        var single = tournament.AddDivision("Men's Division IAA", "Male");
        var empty = tournament.AddDivision("Men's Division IAAA", "Male");
        for (var i = 0; i < 3; i++)
            tournament.EnterTeam(Guid.NewGuid(), busy.Id);
        tournament.EnterTeam(Guid.NewGuid(), single.Id);

        var three = Assert.Throws<DomainRuleException>(() => tournament.RemoveDivision(busy.Id));
        Assert.Equal("This division has 3 teams. Move or remove them on the Teams page first.", three.Message);
        var one = Assert.Throws<DomainRuleException>(() => tournament.RemoveDivision(single.Id));
        Assert.Equal("This division has 1 team. Move or remove them on the Teams page first.", one.Message);

        tournament.RemoveDivision(empty.Id);
        Assert.Equal(2, tournament.Divisions.Count);
    }

    [Theory]
    [InlineData("M", DivisionGender.Male)]
    [InlineData("m", DivisionGender.Male)]
    [InlineData(" male ", DivisionGender.Male)]
    [InlineData("MALE", DivisionGender.Male)]
    [InlineData("F", DivisionGender.Female)]
    [InlineData("female", DivisionGender.Female)]
    [InlineData("Female", DivisionGender.Female)]
    public void ImportGenderAcceptsLettersAndWordsInAnyCase(string value, DivisionGender expected) =>
        Assert.Equal(expected, Division.ParseImportGender(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("Coed")]
    [InlineData("Men")]
    public void ImportGenderRejectsAnythingElse(string? value) =>
        Assert.Null(Division.ParseImportGender(value));

    [Fact]
    public void DivisionLabelShowsNameAndGender() =>
        Assert.Equal("14U Open (Female)", Division.Label("14U Open", "Female"));

    private static Tournament Tournament() =>
        Domain.Tournament.Create(Guid.NewGuid(), "City Cup", null, Start, End, "America/Phoenix", 60);
}