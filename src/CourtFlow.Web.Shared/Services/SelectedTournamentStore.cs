using CourtFlow.ApiClient;
using Microsoft.JSInterop;

namespace CourtFlow.Web.Shared.Services;

public sealed class SelectedTournamentStore(CourtFlowApiClient api, IJSRuntime js)
{
    private readonly CourtFlowApiClient _api = api;
    private readonly IJSRuntime _js = js;
    private const string SelectedTournamentStorageKey = "courtflow.selectedTournamentId";
    private bool _initialized;
    private bool _loadedFromStorage;
    private IReadOnlyList<TournamentDto> _tournaments = Array.Empty<TournamentDto>();

    public Guid? SelectedTournamentId { get; private set; }

    public IReadOnlyList<TournamentDto> Tournaments => _tournaments;

    public event Action? Changed;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized && _loadedFromStorage)
            return;

        if (!_initialized)
        {
            _initialized = true;
            _tournaments = await _api.Tournaments.ListAsync(cancellationToken);
        }

        if (!_loadedFromStorage)
        {
            var (loadedFromStorage, selectedFromStorage) = await TryLoadSelectedFromStorageAsync();
            if (loadedFromStorage)
            {
                _loadedFromStorage = true;
                SelectedTournamentId = selectedFromStorage;
            }
        }

        var previousSelection = SelectedTournamentId;

        if (SelectedTournamentId is not null && _tournaments.All(t => t.Id != SelectedTournamentId.Value))
            SelectedTournamentId = null;

        if (SelectedTournamentId is null)
            SelectedTournamentId = GetLatestTournamentId();

        if (SelectedTournamentId != previousSelection)
        {
            _ = PersistSelectedToStorageAsync(SelectedTournamentId);
            Changed?.Invoke();
        }
    }

    public Task SetSelectedAsync(Guid? id)
    {
        if (SelectedTournamentId == id)
            return Task.CompletedTask;

        SelectedTournamentId = id;
        _ = PersistSelectedToStorageAsync(id);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private async Task<(bool LoadedFromStorage, Guid? SelectedId)> TryLoadSelectedFromStorageAsync()
    {
        try
        {
            var storedValue = await _js.InvokeAsync<string?>("localStorage.getItem", SelectedTournamentStorageKey);
            var parsed = Guid.TryParse(storedValue, out var selectedId) ? selectedId : (Guid?)null;
            return (true, parsed);
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender.
            return (false, null);
        }
        catch (JSDisconnectedException)
        {
            return (false, null);
        }
    }

    private async Task PersistSelectedToStorageAsync(Guid? id)
    {
        try
        {
            if (id is null)
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", SelectedTournamentStorageKey);
                return;
            }

            await _js.InvokeVoidAsync("localStorage.setItem", SelectedTournamentStorageKey, id.Value.ToString());
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender.
        }
        catch (JSDisconnectedException)
        {
            // Ignore disconnects during navigation/reload.
        }
    }

    private Guid? GetLatestTournamentId() =>
        _tournaments
            .OrderByDescending(t => t.StartDate)
            .ThenByDescending(t => t.EndDate)
            .ThenByDescending(t => t.Name)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefault();
}
