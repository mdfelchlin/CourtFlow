using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class TournamentRulesTests
{
    private static readonly Guid OrganizationId = Guid.Parse("8f1c2a3e-5b6d-4e7f-9a0b-1c2d3e4f5a6b");
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 3);

    [Fact]
    public void Whitespace_name_is_rejected_and_a_real_name_is_trimmed()
    {
        Assert.Throws<DomainRuleException>(() => Create("   "));
        var tournament = Create("  City Cup  ");
        Assert.Equal("City Cup", tournament.Name);
    }

    [Fact]
    public void Empty_description_is_saved()
    {
        var tournament = Create("City Cup", description: "   ");
        Assert.Equal("", tournament.Description);
    }

    [Theory]
    [InlineData(2026, 9, 1, 2026, 9, 1)]
    [InlineData(2026, 9, 3, 2026, 9, 1)]
    public void End_date_on_or_before_start_is_rejected(int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
    {
        Assert.Throws<DomainRuleException>(() => Create(
            "City Cup",
            start: new DateOnly(startYear, startMonth, startDay),
            end: new DateOnly(endYear, endMonth, endDay)));
    }

    [Fact]
    public void Span_longer_than_five_days_is_accepted()
    {
        var tournament = Create("City Cup", start: new DateOnly(2026, 9, 1), end: new DateOnly(2026, 9, 6));
        Assert.Equal(new DateOnly(2026, 9, 6), tournament.EndDate);
    }

    [Fact]
    public void September_1_through_5_does_not_warn_and_through_6_does()
    {
        Assert.False(TournamentWarnings.SpanExceedsFiveDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5)));
        Assert.Equal(5, TournamentWarnings.InclusiveDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5)));
        Assert.True(TournamentWarnings.SpanExceedsFiveDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6)));
    }

    [Fact]
    public void Matching_name_warns_except_the_tournaments_own_name()
    {
        Assert.True(TournamentWarnings.NameMatchesAnother("  Cup ", ["cup"]));
        var others = new[] { "Cup", "Open" }.Where(name => !string.Equals(name.Trim(), "Cup", StringComparison.OrdinalIgnoreCase));
        Assert.False(TournamentWarnings.NameMatchesAnother("Cup", others));
        Assert.False(TournamentWarnings.NameMatchesAnother("   ", ["cup"]));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(240)]
    public void Duration_boundaries_are_accepted(int minutes)
    {
        var tournament = Create("City Cup", duration: minutes);
        Assert.Equal(minutes, tournament.DefaultMatchDurationMinutes);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(241)]
    public void Duration_outside_range_is_rejected(int minutes)
    {
        Assert.Throws<DomainRuleException>(() => Create("City Cup", duration: minutes));
    }

    [Fact]
    public void Listed_us_time_zones_are_accepted_and_others_are_rejected()
    {
        foreach (var zone in UsTimeZones.All)
        {
            var tournament = Create("City Cup", timeZoneId: zone.Id);
            Assert.Equal(zone.Id, tournament.TimeZoneId);
        }

        Assert.Throws<DomainRuleException>(() => Create("City Cup", timeZoneId: "UTC"));
        Assert.Throws<DomainRuleException>(() => Create("City Cup", timeZoneId: "  "));
    }

    [Fact]
    public void Duplicate_tournament_names_are_accepted()
    {
        var first = Create("City Cup");
        var second = Create("city cup");
        Assert.Equal("City Cup", first.Name);
        Assert.Equal("city cup", second.Name);
    }

    private static Tournament Create(
        string? name,
        string? description = null,
        DateOnly? start = null,
        DateOnly? end = null,
        string? timeZoneId = "America/Chicago",
        int? duration = 60) =>
        Tournament.Create(OrganizationId, name, description, start ?? Start, end ?? End, timeZoneId, duration);
}
