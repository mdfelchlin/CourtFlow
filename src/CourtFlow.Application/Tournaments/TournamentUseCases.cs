using CourtFlow.Domain;

namespace CourtFlow.Application.Tournaments;

public sealed class TournamentUseCases(ITournamentRepository repository, IOrganizationScope organization)
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

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(id, cancellationToken);
        if (tournament is null)
            return false;

        repository.Remove(tournament);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static TournamentDetails ToDetails(Tournament tournament) =>
        new(
            tournament.Id,
            tournament.Name,
            tournament.Description,
            tournament.StartDate,
            tournament.EndDate,
            tournament.TimeZoneId,
            tournament.DefaultMatchDurationMinutes);
}
