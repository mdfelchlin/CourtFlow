using CourtFlow.Domain;

namespace CourtFlow.Application.Tournaments;

public sealed class TournamentUseCases(ITournamentRepository repository, IOrganizationScope organization, TimeProvider clock)
{
    public Task<IReadOnlyList<TournamentDetails>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);

    public async Task<TournamentDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(id, cancellationToken);
        return tournament is null ? null : ToDetails(tournament);
    }

    public async Task<TournamentDetails> CreateAsync(SaveTournament command, CancellationToken cancellationToken)
    {
        var tournament = Tournament.Create(
            organization.OrganizationId,
            command.Name,
            command.Description,
            command.StartDate,
            command.EndDate,
            command.TimeZoneId,
            command.DefaultMatchDurationMinutes);
        await repository.AddAsync(tournament, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDetails(tournament);
    }

    public async Task<TournamentDetails?> UpdateAsync(Guid id, SaveTournament command, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(id, cancellationToken);
        if (tournament is null)
            return null;

        Tournament.Validate(
            command.Name,
            command.Description,
            command.StartDate,
            command.EndDate,
            command.TimeZoneId,
            command.DefaultMatchDurationMinutes);
        tournament.Rename(command.Name);
        tournament.SetDescription(command.Description);
        tournament.SetDates(command.StartDate!.Value, command.EndDate!.Value);
        tournament.SetTimeZone(command.TimeZoneId);
        tournament.SetDuration(command.DefaultMatchDurationMinutes!.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDetails(tournament);
    }

    public Task<TournamentDetails?> PublishAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, tournament => tournament.Publish(clock.GetUtcNow()), cancellationToken);

    public Task<TournamentDetails?> UnpublishAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, tournament => tournament.Unpublish(), cancellationToken);

    public Task<TournamentDetails?> ArchiveAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, tournament => tournament.Archive(), cancellationToken);

    public Task<TournamentDetails?> RestoreAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, tournament => tournament.Restore(), cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(id, cancellationToken);
        if (tournament is null)
            return false;

        tournament.EnsureCanDelete();
        repository.Remove(tournament);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Archives every published tournament whose 270 days are up, across all organizations. Returns how many were archived.</summary>
    public async Task<int> ArchiveExpiredAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await repository.ListDueForArchiveAsync(now, cancellationToken);
        var archived = due.Count(tournament => tournament.ArchiveIfDue(now));
        if (archived > 0)
            await repository.SaveChangesAsync(cancellationToken);

        return archived;
    }

    private async Task<TournamentDetails?> TransitionAsync(Guid id, Action<Tournament> transition, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(id, cancellationToken);
        if (tournament is null)
            return null;

        transition(tournament);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDetails(tournament);
    }

    internal static TournamentDetails ToDetails(Tournament tournament) =>
        new(
            tournament.Id,
            tournament.Name,
            tournament.Description,
            tournament.StartDate,
            tournament.EndDate,
            tournament.TimeZoneId,
            tournament.DefaultMatchDurationMinutes,
            tournament.Status,
            tournament.LastPublishedAt);
}
