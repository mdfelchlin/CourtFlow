using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class VenueCourtTests
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 3);

    [Fact]
    public void Whitespace_venue_name_is_rejected_and_a_real_name_is_trimmed()
    {
        var tournament = Tournament();
        Assert.Throws<DomainRuleException>(() => tournament.AddVenue("   ", null, 1));
        var venue = tournament.AddVenue("  Main Gym  ", null, 1);
        Assert.Equal("Main Gym", venue.Name);
    }

    [Fact]
    public void Empty_address_is_saved_and_duplicate_names_are_allowed()
    {
        var tournament = Tournament();
        var first = tournament.AddVenue("Main Gym", "   ", 0);
        var second = tournament.AddVenue("Main Gym", null, 0);
        Assert.Equal("", first.Address);
        Assert.Equal("", second.Address);
        Assert.Equal(2, tournament.Venues.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Court_count_boundaries_are_accepted(int count)
    {
        var venue = Tournament().AddVenue("Main Gym", null, count);
        Assert.Equal(count, venue.Courts.Count);
        Assert.Equal(Enumerable.Range(1, count), venue.Courts.Select(court => court.Number).OrderBy(number => number));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Court_count_outside_range_is_rejected(int count)
    {
        Assert.Throws<DomainRuleException>(() => Tournament().AddVenue("Main Gym", null, count));
    }

    [Fact]
    public void Changing_the_count_keeps_the_courts_whose_numbers_remain()
    {
        var tournament = Tournament();
        var venue = tournament.AddVenue("Main Gym", null, 3);
        var original = venue.Courts.ToDictionary(court => court.Number, court => court.Id);

        tournament.UpdateVenue(venue.Id, "Main Gym", null, 5);
        Assert.Equal(original[1], CourtId(venue, 1));
        Assert.Equal(original[2], CourtId(venue, 2));
        Assert.Equal(original[3], CourtId(venue, 3));
        Assert.Equal([1, 2, 3, 4, 5], venue.Courts.Select(court => court.Number).OrderBy(number => number).ToArray());

        tournament.UpdateVenue(venue.Id, "Main Gym", null, 1);
        Assert.Equal(original[1], CourtId(venue, 1));
        Assert.Single(venue.Courts);

        tournament.UpdateVenue(venue.Id, "Main Gym", null, 0);
        Assert.Empty(venue.Courts);
    }

    private static Tournament Tournament() =>
        Domain.Tournament.Create(Guid.NewGuid(), "City Cup", null, Start, End, "America/Phoenix", 60);

    private static Guid CourtId(Venue venue, int number) =>
        venue.Courts.Single(court => court.Number == number).Id;
}
