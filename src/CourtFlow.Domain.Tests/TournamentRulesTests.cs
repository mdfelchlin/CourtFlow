using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class TournamentRulesTests
{
    private static readonly Guid OrganizationId = Guid.Parse("8f1c2a3e-5b6d-4e7f-9a0b-1c2d3e4f5a6b");
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 3);
    private static readonly string[] CupAndOpen = ["Cup", "Open"];

    [Fact]
    public void WhitespaceNameIsRejectedAndARealNameIsTrimmed()
    {
        Assert.Throws<DomainRuleException>(() => Create("   "));
        var tournament = Create("  City Cup  ");
        Assert.Equal("City Cup", tournament.Name);
    }

    [Fact]
    public void EmptyDescriptionIsSaved()
    {
        var tournament = Create("City Cup", description: "   ");
        Assert.Equal("", tournament.Description);
    }

    [Theory]
    [InlineData(2026, 9, 1, 2026, 9, 1)]
    [InlineData(2026, 9, 3, 2026, 9, 1)]
    public void EndDateOnOrBeforeStartIsRejected(int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
    {
        Assert.Throws<DomainRuleException>(() => Create(
            "City Cup",
            start: new DateOnly(startYear, startMonth, startDay),
            end: new DateOnly(endYear, endMonth, endDay)));
    }

    [Fact]
    public void SpanLongerThanFiveDaysIsAccepted()
    {
        var tournament = Create("City Cup", start: new DateOnly(2026, 9, 1), end: new DateOnly(2026, 9, 6));
        Assert.Equal(new DateOnly(2026, 9, 6), tournament.EndDate);
    }

    [Fact]
    public void September1Through5DoesNotWarnAndThrough6Does()
    {
        Assert.False(TournamentWarnings.SpanExceedsFiveDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5)));
        Assert.Equal(5, TournamentWarnings.InclusiveDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5)));
        Assert.True(TournamentWarnings.SpanExceedsFiveDays(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6)));
    }

    [Fact]
    public void MatchingNameWarnsExceptTheTournamentsOwnName()
    {
        Assert.True(TournamentWarnings.NameMatchesAnother("  Cup ", ["cup"]));
        var others = CupAndOpen.Where(name => !string.Equals(name.Trim(), "Cup", StringComparison.OrdinalIgnoreCase));
        Assert.False(TournamentWarnings.NameMatchesAnother("Cup", others));
        Assert.False(TournamentWarnings.NameMatchesAnother("   ", ["cup"]));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(240)]
    public void DurationBoundariesAreAccepted(int minutes)
    {
        var tournament = Create("City Cup", duration: minutes);
        Assert.Equal(minutes, tournament.DefaultMatchDurationMinutes);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(241)]
    public void DurationOutsideRangeIsRejected(int minutes)
    {
        Assert.Throws<DomainRuleException>(() => Create("City Cup", duration: minutes));
    }

    [Fact]
    public void ListedUsTimeZonesAreAcceptedAndOthersAreRejected()
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
    public void DuplicateTournamentNamesAreAccepted()
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