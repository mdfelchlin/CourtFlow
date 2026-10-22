using CourtFlow.Web.Services;

namespace CourtFlow.Web.Tests;

public sealed class FakeTournamentMemory : ITournamentMemory
{
    public Guid? Remembered { get; set; }
    public int Forgets { get; private set; }

    public Task<Guid?> ReadAsync() => Task.FromResult(Remembered);

    public Task RememberAsync(Guid tournamentId)
    {
        Remembered = tournamentId;
        return Task.CompletedTask;
    }

    public Task ForgetAsync()
    {
        Forgets++;
        Remembered = null;
        return Task.CompletedTask;
    }
}