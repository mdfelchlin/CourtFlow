using CourtFlow.Domain;

namespace CourtFlow.Application.Divisions;

public sealed class DivisionUseCases(ITournamentRepository repository)
{
    public async Task<IReadOnlyList<DivisionDetails>?> ListAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        return Division.OrderForList(tournament.Divisions, division => division.Name, division => division.Gender.ToString())
            .Select(ToDetails)
            .ToList();
    }

    public async Task<DivisionDetails?> CreateAsync(Guid tournamentId, SaveDivision command, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        var division = tournament.AddDivision(command.Name, command.Gender);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDetails(division);
    }

    public async Task<DivisionDetails?> UpdateAsync(Guid divisionId, SaveDivision command, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetByDivisionAsync(divisionId, cancellationToken);
        if (tournament is null)
            return null;

        var division = tournament.UpdateDivision(divisionId, command.Name, command.Gender);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDetails(division);
    }

    public async Task<bool> DeleteAsync(Guid divisionId, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetByDivisionAsync(divisionId, cancellationToken);
        if (tournament is null)
            return false;

        tournament.RemoveDivision(divisionId);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static DivisionDetails ToDetails(Division division) =>
        new(division.Id, division.TournamentId, division.Name, division.Gender.ToString());
}
