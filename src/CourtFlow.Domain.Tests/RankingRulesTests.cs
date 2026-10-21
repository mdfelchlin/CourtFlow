using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class RankingRulesTests
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly End = new(2026, 9, 3);

    [Fact]
    public void SavingRequiresExactlyTheDivisionsEntriesAndThenRanksThemInOrder()
    {
        var tournament = Tournament();
        var division = tournament.AddDivision("14U Open", "Female");
        var entries = Enter(tournament, division.Id, 3);
        var before = Ranks(tournament, division.Id);

        var missing = Assert.Throws<DomainRuleException>(() => tournament.SaveRanking(division.Id, entries.Take(2).ToArray()));
        var extra = Assert.Throws<DomainRuleException>(() => tournament.SaveRanking(division.Id, [.. entries, Guid.NewGuid()]));
        var repeated = Assert.Throws<DomainRuleException>(() => tournament.SaveRanking(division.Id, [entries[0], entries[0], entries[1]]));

        Assert.Equal(Domain.Tournament.TeamsChanged, missing.Message);
        Assert.Equal(Domain.Tournament.TeamsChanged, extra.Message);
        Assert.Equal(Domain.Tournament.TeamsChanged, repeated.Message);
        Assert.Equal(before, Ranks(tournament, division.Id));
        Assert.False(tournament.IsRanked(division.Id));

        tournament.SaveRanking(division.Id, [entries[2], entries[0], entries[1]]);

        Assert.Equal([entries[2], entries[0], entries[1]], Ranks(tournament, division.Id));
        Assert.Equal([1, 2, 3], tournament.RankingOf(division.Id).Select(place => place.Rank).ToArray());
        Assert.Equal(0, tournament.NewTeamCount(division.Id));
    }

    [Fact]
    public void AddingAndMovingInAppendAsNewAndRemovingClosesTheGap()
    {
        var tournament = Tournament();
        var open = tournament.AddDivision("14U Open", "Female");
        var gold = tournament.AddDivision("14U Gold", "Female");
        var entries = Enter(tournament, open.Id, 2);
        tournament.SaveRanking(open.Id, entries);
        var movedIn = tournament.EnterTeam(Guid.NewGuid(), gold.Id);
        tournament.SaveRanking(gold.Id, [movedIn.Id]);

        var first = tournament.EnterTeam(Guid.NewGuid(), open.Id);
        var second = tournament.EnterTeam(Guid.NewGuid(), open.Id);
        Assert.Equal([.. entries, first.Id, second.Id], Ranks(tournament, open.Id));
        Assert.Equal([false, false, true, true], tournament.RankingOf(open.Id).Select(place => place.IsNew).ToArray());

        var fromGold = tournament.MoveEntry(movedIn.Id, open.Id);
        Assert.Equal(fromGold.Id, movedIn.Id);
        Assert.Equal([.. entries, first.Id, second.Id, movedIn.Id], Ranks(tournament, open.Id));
        Assert.True(tournament.RankingOf(open.Id).Single(place => place.EntryId == movedIn.Id).IsNew);
        Assert.False(tournament.IsRanked(gold.Id));

        tournament.RemoveEntry(entries[0]);
        Assert.Equal([entries[1], first.Id, second.Id, movedIn.Id], Ranks(tournament, open.Id));
        Assert.Equal([1, 2, 3, 4], tournament.RankingOf(open.Id).Select(place => place.Rank).ToArray());
        Assert.Equal(3, tournament.NewTeamCount(open.Id));
    }

    [Fact]
    public void SavingClearsNewTeamsAndTheLastTeamLeavingUnranksTheDivision()
    {
        var tournament = Tournament();
        var division = tournament.AddDivision("14U Open", "Female");
        var first = tournament.EnterTeam(Guid.NewGuid(), division.Id);
        tournament.SaveRanking(division.Id, [first.Id]);
        var added = tournament.EnterTeam(Guid.NewGuid(), division.Id);
        Assert.Equal(1, tournament.NewTeamCount(division.Id));

        tournament.SaveRanking(division.Id, [added.Id, first.Id]);
        Assert.Equal(0, tournament.NewTeamCount(division.Id));
        Assert.True(tournament.IsRanked(division.Id));

        tournament.RemoveEntry(added.Id);
        tournament.RemoveEntry(first.Id);
        Assert.False(tournament.IsRanked(division.Id));
        Assert.Empty(tournament.RankingOf(division.Id));

        var later = tournament.EnterTeam(Guid.NewGuid(), division.Id);
        Assert.False(tournament.IsRanked(division.Id));
        Assert.DoesNotContain(tournament.Rankings, place => place.EntryId == later.Id);
    }

    [Fact]
    public void AnUnrankedDivisionStoresNothingWhenItsTeamsChange()
    {
        var tournament = Tournament();
        var open = tournament.AddDivision("14U Open", "Female");
        var gold = tournament.AddDivision("14U Gold", "Female");
        var entry = tournament.EnterTeam(Guid.NewGuid(), open.Id);
        tournament.MoveEntry(entry.Id, gold.Id);
        tournament.MoveEntry(entry.Id, open.Id);
        tournament.RemoveEntry(entry.Id);

        Assert.Empty(tournament.Rankings);
    }

    [Fact]
    public void ATeamThatLeavesAndReturnsIsNewAgainAtTheBottom()
    {
        var tournament = Tournament();
        var open = tournament.AddDivision("14U Open", "Female");
        var gold = tournament.AddDivision("14U Gold", "Female");
        var staying = tournament.EnterTeam(Guid.NewGuid(), open.Id);
        var leaving = tournament.EnterTeam(Guid.NewGuid(), open.Id);
        tournament.SaveRanking(open.Id, [staying.Id, leaving.Id]);

        tournament.MoveEntry(leaving.Id, gold.Id);
        Assert.Equal([staying.Id], Ranks(tournament, open.Id));
        Assert.False(tournament.IsRanked(gold.Id));

        tournament.MoveEntry(leaving.Id, open.Id);
        Assert.Equal([staying.Id, leaving.Id], Ranks(tournament, open.Id));
        Assert.True(tournament.RankingOf(open.Id).Single(place => place.EntryId == leaving.Id).IsNew);
    }

    [Fact]
    public void MovingWithinADivisionOrRenamingItLeavesTheRankingAlone()
    {
        var tournament = Tournament();
        var division = tournament.AddDivision("14U Open", "Female");
        var entries = Enter(tournament, division.Id, 2);
        tournament.SaveRanking(division.Id, entries);
        var added = tournament.EnterTeam(Guid.NewGuid(), division.Id);

        tournament.MoveEntry(entries[0], division.Id);
        tournament.UpdateDivision(division.Id, "14U Gold", "Male");

        Assert.Equal([.. entries, added.Id], Ranks(tournament, division.Id));
        Assert.Equal([false, false, true], tournament.RankingOf(division.Id).Select(place => place.IsNew).ToArray());
    }

    [Fact]
    public void AnArchivedTournamentCannotSaveARanking()
    {
        var tournament = Tournament();
        var division = tournament.AddDivision("14U Open", "Female");
        var entry = tournament.EnterTeam(Guid.NewGuid(), division.Id);
        tournament.Publish(DateTimeOffset.Parse("2026-09-01T15:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        tournament.Archive();

        var rejected = Assert.Throws<DomainRuleException>(() => tournament.SaveRanking(division.Id, [entry.Id]));
        Assert.Equal(Domain.Tournament.ArchivedMessage, rejected.Message);
        Assert.False(tournament.IsRanked(division.Id));
    }

    [Fact]
    public void SeveralTeamsAddedTogetherLandAtTheBottomInThatOrder()
    {
        var tournament = Tournament();
        var division = tournament.AddDivision("14U Open", "Female");
        var saved = Enter(tournament, division.Id, 1);
        tournament.SaveRanking(division.Id, saved);
        var added = Enter(tournament, division.Id, 3);

        Assert.Equal([.. saved, .. added], Ranks(tournament, division.Id));
        Assert.Equal(3, tournament.NewTeamCount(division.Id));
        Assert.All(tournament.RankingOf(division.Id).Skip(1), place => Assert.True(place.IsNew));
    }

    private static Guid[] Enter(Tournament tournament, Guid divisionId, int count)
    {
        var ids = new Guid[count];
        for (var index = 0; index < count; index++)
            ids[index] = tournament.EnterTeam(Guid.NewGuid(), divisionId).Id;
        return ids;
    }

    private static Guid[] Ranks(Tournament tournament, Guid divisionId) =>
        tournament.RankingOf(divisionId).Select(place => place.EntryId).ToArray();

    private static Tournament Tournament() =>
        Domain.Tournament.Create(Guid.NewGuid(), "City Cup", null, Start, End, "America/Phoenix", 60);
}