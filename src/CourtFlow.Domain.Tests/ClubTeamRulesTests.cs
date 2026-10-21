using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class ClubTeamRulesTests
{
    private static readonly Guid Organization = Guid.NewGuid();

    [Fact]
    public void BlankClubNameIsRejectedAndARealNameIsTrimmed()
    {
        var blank = Assert.Throws<DomainRuleException>(() => Club.Create(Organization, "   "));
        Assert.Equal(Club.NameRequired, blank.Message);

        var club = Club.Create(Organization, "  Ohio State University  ");
        Assert.Equal("Ohio State University", club.Name);
        Assert.Equal(Club.Normalize(" ohio state university "), club.NormalizedName);
    }

    [Fact]
    public void BlankTeamNameIsRejectedAndARealNameIsTrimmed()
    {
        var club = Club.Create(Organization, "Ohio State University");

        var blank = Assert.Throws<DomainRuleException>(() => club.AddTeam(" "));
        Assert.Equal(Team.NameRequired, blank.Message);

        var team = club.AddTeam("  Ohio St U  ");
        Assert.Equal("Ohio St U", team.Name);
        Assert.Equal(club.Id, team.ClubId);
    }

    [Fact]
    public void TeamNamesMatchIgnoringCaseAndSpacesWithinAClub()
    {
        var club = Club.Create(Organization, "Ohio State University");
        var team = club.AddTeam("Ohio St U");

        Assert.Same(team, club.FindTeam(" ohio st u "));
        var duplicate = Assert.Throws<DomainRuleException>(() => club.AddTeam(" OHIO ST U "));
        Assert.Equal(Team.DuplicateName, duplicate.Message);
    }

    [Fact]
    public void TwoClubsMayEachHaveATeamWithTheSameName()
    {
        var first = Club.Create(Organization, "Ohio State University").AddTeam("Ohio St U");
        var second = Club.Create(Organization, "Ohio University").AddTeam("Ohio St U");

        Assert.Equal(first.Name, second.Name);
        Assert.NotEqual(first.ClubId, second.ClubId);
    }

    [Fact]
    public void RenamingToAnotherTeamsNameIsRejectedButACaseChangeIsAllowed()
    {
        var club = Club.Create(Organization, "Purdue University");
        var a = club.AddTeam("Purdue A");
        club.AddTeam("Purdue B");

        var collision = Assert.Throws<DomainRuleException>(() => club.RenameTeam(a.Id, " purdue b "));
        Assert.Equal(Team.DuplicateName, collision.Message);
        Assert.Equal("Purdue A", a.Name);

        club.RenameTeam(a.Id, "PURDUE A");
        Assert.Equal("PURDUE A", a.Name);
    }

    [Fact]
    public void RenamingKeepsTheTeamInItsClub()
    {
        var club = Club.Create(Organization, "Purdue University");
        var team = club.AddTeam("Purdue A");

        club.RenameTeam(team.Id, "Purdue Gold");

        Assert.Equal(club.Id, team.ClubId);
        Assert.Equal("Purdue Gold", team.Name);
    }
}