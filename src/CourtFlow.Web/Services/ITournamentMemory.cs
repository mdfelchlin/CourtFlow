namespace CourtFlow.Web.Services;

/// <summary>Remembers the last tournament the user worked on, across visits.</summary>
public interface ITournamentMemory
{
    Task<Guid?> ReadAsync();
    Task RememberAsync(Guid tournamentId);
    Task ForgetAsync();
}