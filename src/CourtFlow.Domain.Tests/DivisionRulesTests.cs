using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class DivisionRulesTests
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 3);

    [Fact]
    public void Whitespace_name_is_rejected_and_a_real_name_is_trimmed()
    {
        var tournament = Tournament();
        var blank = Assert.Throws<DomainRuleException>(() => tournament.AddDivision("   ", "Female"));
        Assert.Equal(Division.NameRequired, blank.Message);

        var division = tournament.AddDivision("  14U Open  ", "Female");
        Assert.Equal("14U Open", division.Name);
        Assert.Equal(DivisionGender.Female, division.Gender);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("female")]
    [InlineData("Coed")]
    public void Gender_other_than_male_or_female_is_rejected(string? gender)
    {
        var error = Assert.Throws<DomainRuleException>(() => Tournament().AddDivision("14U Open", gender));
        Assert.Equal(Division.GenderRequired, error.Message);
    }

    [Fact]
    public void The_same_name_is_allowed_once_per_gender()
    {
        var tournament = Tournament();
        tournament.AddDivision("14U Open", "Female");
        tournament.AddDivision("14U Open", "Male");
        Assert.Equal(2, tournament.Divisions.Count);

        var duplicate = Assert.Throws<DomainRuleException>(() => tournament.AddDivision(" 14u open ", "Female"));
        Assert.Equal(Division.DuplicateNameAndGender, duplicate.Message);
    }

    [Fact]
    public void Updating_a_division_does_not_conflict_with_itself()
    {
        var tournament = Tournament();
        var female = tournament.AddDivision("14U Open", "Female");
        var male = tournament.AddDivision("14U Open", "Male");

        tournament.UpdateDivision(female.Id, "  14U Open  ", "Female");
        Assert.Equal("14U Open", female.Name);
        Assert.Equal(DivisionGender.Female, female.Gender);

        var conflict = Assert.Throws<DomainRuleException>(() => tournament.UpdateDivision(female.Id, "14U Open", "Male"));
        Assert.Equal(Division.DuplicateNameAndGender, conflict.Message);
        Assert.Equal(DivisionGender.Female, female.Gender);
        Assert.Equal(DivisionGender.Male, male.Gender);
    }

    [Fact]
    public void Another_tournament_may_reuse_the_same_name_and_gender()
    {
        Tournament().AddDivision("14U Open", "Female");
        var other = Tournament().AddDivision("14U Open", "Female");
        Assert.Equal("14U Open", other.Name);
    }

    [Fact]
    public void Removing_a_division_leaves_the_tournament_and_its_other_divisions()
    {
        var tournament = Tournament();
        var female = tournament.AddDivision("14U Open", "Female");
        var male = tournament.AddDivision("14U Open", "Male");

        tournament.RemoveDivision(female.Id);

        Assert.Equal(tournament.Id, tournament.Id);
        Assert.Equal([male.Id], tournament.Divisions.Select(division => division.Id).ToArray());
    }

    [Fact]
    public void List_order_is_name_then_female_before_male()
    {
        var ordered = Division.OrderForList(
            [
                ("14U Open", "Male"),
                ("12U", "Male"),
                ("14U Open", "Female"),
                ("12u", "Female"),
            ],
            item => item.Item1,
            item => item.Item2);

        Assert.Equal(
            [("12u", "Female"), ("12U", "Male"), ("14U Open", "Female"), ("14U Open", "Male")],
            ordered);
    }

    private static Tournament Tournament() =>
        Domain.Tournament.Create(Guid.NewGuid(), "City Cup", null, Start, End, "America/Phoenix", 60);
}
