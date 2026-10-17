using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class TournamentLifecycleTests
{
    private static readonly Guid OrganizationId = Guid.Parse("8f1c2a3e-5b6d-4e7f-9a0b-1c2d3e4f5a6b");
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_tournament_is_unpublished_with_no_publish_moment()
    {
        var tournament = Create();

        Assert.Equal(TournamentStatus.Unpublished, tournament.Status);
        Assert.Null(tournament.LastPublishedAt);
        Assert.Null(tournament.ArchiveDueAt);
    }

    [Theory]
    [InlineData(TournamentStatus.Unpublished, "publish", TournamentStatus.Published)]
    [InlineData(TournamentStatus.Published, "unpublish", TournamentStatus.Unpublished)]
    [InlineData(TournamentStatus.Published, "archive", TournamentStatus.Archived)]
    [InlineData(TournamentStatus.Archived, "restore", TournamentStatus.Unpublished)]
    public void Allowed_transitions_move_to_the_expected_state(TournamentStatus from, string action, TournamentStatus expected)
    {
        var tournament = CreateIn(from);

        Apply(tournament, action);

        Assert.Equal(expected, tournament.Status);
    }

    [Theory]
    [InlineData(TournamentStatus.Published, "publish", Tournament.OnlyUnpublishedCanBePublished)]
    [InlineData(TournamentStatus.Archived, "publish", Tournament.OnlyUnpublishedCanBePublished)]
    [InlineData(TournamentStatus.Unpublished, "unpublish", Tournament.OnlyPublishedCanBeUnpublished)]
    [InlineData(TournamentStatus.Archived, "unpublish", Tournament.OnlyPublishedCanBeUnpublished)]
    [InlineData(TournamentStatus.Unpublished, "archive", Tournament.OnlyPublishedCanBeArchived)]
    [InlineData(TournamentStatus.Archived, "archive", Tournament.OnlyPublishedCanBeArchived)]
    [InlineData(TournamentStatus.Unpublished, "restore", Tournament.OnlyArchivedCanBeRestored)]
    [InlineData(TournamentStatus.Published, "restore", Tournament.OnlyArchivedCanBeRestored)]
    public void Every_other_transition_is_rejected_and_the_state_is_unchanged(TournamentStatus from, string action, string message)
    {
        var tournament = CreateIn(from);

        var exception = Assert.Throws<DomainRuleException>(() => Apply(tournament, action));

        Assert.Equal(message, exception.Message);
        Assert.Equal(from, tournament.Status);
    }

    [Fact]
    public void Publish_records_the_moment_and_republishing_moves_it()
    {
        var tournament = Create();

        tournament.Publish(Noon);
        Assert.Equal(Noon, tournament.LastPublishedAt);
        Assert.Equal(Noon + Tournament.AutoArchiveAfter, tournament.ArchiveDueAt);

        tournament.Unpublish();
        Assert.Equal(Noon, tournament.LastPublishedAt);

        var later = Noon.AddDays(100);
        tournament.Publish(later);
        Assert.Equal(later, tournament.LastPublishedAt);
        Assert.Equal(later + Tournament.AutoArchiveAfter, tournament.ArchiveDueAt);
    }

    [Fact]
    public void Archive_and_restore_leave_the_publish_moment_alone()
    {
        var tournament = Create();
        tournament.Publish(Noon);

        tournament.Archive();
        Assert.Equal(Noon, tournament.LastPublishedAt);

        tournament.Restore();
        Assert.Equal(Noon, tournament.LastPublishedAt);
        Assert.Null(tournament.ArchiveDueAt);
    }

    [Fact]
    public void The_automatic_archive_waits_the_full_270_days()
    {
        var tournament = Create();
        tournament.Publish(Noon);
        var due = Noon + TimeSpan.FromDays(270);

        Assert.False(tournament.ArchiveIfDue(due.AddSeconds(-1)));
        Assert.Equal(TournamentStatus.Published, tournament.Status);

        Assert.True(tournament.ArchiveIfDue(due));
        Assert.Equal(TournamentStatus.Archived, tournament.Status);
    }

    [Fact]
    public void The_automatic_archive_leaves_unpublished_and_archived_tournaments_alone()
    {
        var unpublished = Create();
        Assert.False(unpublished.ArchiveIfDue(Noon.AddYears(5)));
        Assert.Equal(TournamentStatus.Unpublished, unpublished.Status);

        var archived = Create();
        archived.Publish(Noon);
        archived.Archive();
        Assert.False(archived.ArchiveIfDue(Noon.AddYears(5)));
        Assert.Equal(TournamentStatus.Archived, archived.Status);
    }

    [Fact]
    public void The_automatic_archive_counts_from_the_latest_publish()
    {
        var tournament = Create();
        tournament.Publish(Noon);
        tournament.Unpublish();
        tournament.Publish(Noon.AddDays(100));

        Assert.False(tournament.ArchiveIfDue(Noon + TimeSpan.FromDays(270)));
        Assert.True(tournament.ArchiveIfDue(Noon + TimeSpan.FromDays(370)));
    }

    [Theory]
    [InlineData(TournamentStatus.Unpublished)]
    [InlineData(TournamentStatus.Archived)]
    public void Unpublished_and_archived_tournaments_can_be_deleted(TournamentStatus status)
    {
        CreateIn(status).EnsureCanDelete();
    }

    [Fact]
    public void A_published_tournament_cannot_be_deleted()
    {
        var exception = Assert.Throws<DomainRuleException>(() => CreateIn(TournamentStatus.Published).EnsureCanDelete());

        Assert.Equal(Tournament.PublishedCannotBeDeleted, exception.Message);
    }

    [Fact]
    public void An_archived_tournament_rejects_every_change()
    {
        var tournament = CreateIn(TournamentStatus.Archived);

        foreach (var change in Changes(tournament))
        {
            var exception = Assert.Throws<DomainRuleException>(change);
            Assert.Equal(Tournament.ArchivedMessage, exception.Message);
        }
    }

    [Fact]
    public void A_restored_tournament_accepts_changes_again()
    {
        var tournament = CreateIn(TournamentStatus.Archived);

        tournament.Restore();

        tournament.Rename("Spring Classic");
        var venue = tournament.AddVenue("Main Gym", "", 2);
        var division = tournament.AddDivision("14U Open", nameof(DivisionGender.Female));
        var entry = tournament.EnterTeam(Guid.NewGuid(), division.Id);

        Assert.Equal("Spring Classic", tournament.Name);
        Assert.Equal(2, venue.Courts.Count);
        Assert.Single(tournament.Entries);
        tournament.RemoveEntry(entry.Id);
        Assert.Empty(tournament.Entries);
    }

    /// <summary>Every change an archived tournament must refuse. Ids are arbitrary: the lock runs before anything is looked up.</summary>
    private static IEnumerable<Action> Changes(Tournament tournament)
    {
        yield return () => tournament.Rename("Spring Classic");
        yield return () => tournament.SetDescription("Anything");
        yield return () => tournament.SetDates(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));
        yield return () => tournament.SetTimeZone("America/Denver");
        yield return () => tournament.SetDuration(45);
        yield return () => tournament.AddVenue("Main Gym", "", 2);
        yield return () => tournament.UpdateVenue(Guid.NewGuid(), "Main Gym", "", 2);
        yield return () => tournament.RemoveVenue(Guid.NewGuid());
        yield return () => tournament.AddDivision("14U Open", nameof(DivisionGender.Female));
        yield return () => tournament.UpdateDivision(Guid.NewGuid(), "14U Open", nameof(DivisionGender.Female));
        yield return () => tournament.RemoveDivision(Guid.NewGuid());
        yield return () => tournament.EnterTeam(Guid.NewGuid(), Guid.NewGuid());
        yield return () => tournament.MoveEntry(Guid.NewGuid(), Guid.NewGuid());
        yield return () => tournament.RemoveEntry(Guid.NewGuid());
    }

    private static void Apply(Tournament tournament, string action)
    {
        switch (action)
        {
            case "publish":
                tournament.Publish(Noon);
                break;
            case "unpublish":
                tournament.Unpublish();
                break;
            case "archive":
                tournament.Archive();
                break;
            case "restore":
                tournament.Restore();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown action.");
        }
    }

    private static Tournament CreateIn(TournamentStatus status)
    {
        var tournament = Create();
        if (status == TournamentStatus.Unpublished)
            return tournament;

        tournament.Publish(Noon);
        if (status == TournamentStatus.Archived)
            tournament.Archive();

        return tournament;
    }

    private static Tournament Create() =>
        Tournament.Create(
            OrganizationId,
            "City Cup",
            "",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            "America/Chicago",
            60);
}
