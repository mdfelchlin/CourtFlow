using Microsoft.JSInterop;

namespace CourtFlow.Web.Services;

/// <summary>Keeps the remembered tournament in local storage through <c>wwwroot/js/tournament-memory.js</c>.</summary>
public sealed class JsTournamentMemory(IJSRuntime js) : ITournamentMemory
{
    public async Task<Guid?> ReadAsync()
    {
        var value = await js.InvokeAsync<string?>("courtflowTournamentMemory.read");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public async Task RememberAsync(Guid tournamentId) =>
        await js.InvokeVoidAsync("courtflowTournamentMemory.write", tournamentId.ToString());

    public async Task ForgetAsync() =>
        await js.InvokeVoidAsync("courtflowTournamentMemory.clear");
}
