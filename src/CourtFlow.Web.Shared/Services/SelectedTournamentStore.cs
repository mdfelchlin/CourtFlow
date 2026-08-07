using CourtFlow.ApiClient;
using Microsoft.JSInterop;

namespace CourtFlow.Web.Shared.Services;

public sealed class SelectedTournamentStore(CourtFlowApiClient api, IJSRuntime js)
{
    private readonly CourtFlowApiClient _api = api;
    private readonly IJSRuntime _js = js;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private const string SelectedTournamentStorageKey = "courtflow.selectedTournamentId";
    private bool _initialized;
    private bool _loadedFromStorage;
    private IReadOnlyList<TournamentDto> _tournaments = Array.Empty<TournamentDto>();

    public Guid? SelectedTournamentId { get; private set; }

    public DivisionFormatDto DefaultPlayFormat { get; private set; } = DivisionFormatDto.PoolPlusBracket;

    public IReadOnlyList<TournamentDto> Tournaments => _tournaments;

    /// <summary>
    /// Bumped when the tournament catalog or metadata changes so layout can remount scoped pages.
    /// </summary>
    public int CatalogVersion { get; private set; }

    public event Action? Changed;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized && _loadedFromStorage)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized && _loadedFromStorage)
                return;

            if (!_initialized)
            {
                _tournaments = await _api.Tournaments.ListAsync(cancellationToken);
                _initialized = true;
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
            NormalizeSelection();

            if (SelectedTournamentId != previousSelection)
            {
                _ = PersistSelectedToStorageAsync(SelectedTournamentId);
                Changed?.Invoke();
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task SetSelectedAsync(Guid? id)
    {
        if (SelectedTournamentId == id)
            return Task.CompletedTask;

        SelectedTournamentId = id;
        SyncDefaultPlayFormatFromSelection();
        _ = PersistSelectedToStorageAsync(id);
        CatalogVersion++;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public void SetDefaultPlayFormat(DivisionFormatDto format)
    {
        if (DefaultPlayFormat == format)
            return;

        DefaultPlayFormat = format;
        Changed?.Invoke();
    }

    public async Task RefreshTournamentsAsync(Guid? selectId = null, CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            _tournaments = await _api.Tournaments.ListAsync(cancellationToken);
            _initialized = true;

            var previousSelection = SelectedTournamentId;
            if (selectId is not null)
                SelectedTournamentId = selectId;
            else
                NormalizeSelection();

            SyncDefaultPlayFormatFromSelection();

            if (SelectedTournamentId != previousSelection)
                _ = PersistSelectedToStorageAsync(SelectedTournamentId);

            CatalogVersion++;
            Changed?.Invoke();
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void NormalizeSelection()
    {
        if (_tournaments.Count == 0)
            return;

        if (SelectedTournamentId is not null && _tournaments.All(t => t.Id != SelectedTournamentId.Value))
            SelectedTournamentId = null;

        if (SelectedTournamentId is null)
            SelectedTournamentId = GetLatestTournamentId();
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

    private void SyncDefaultPlayFormatFromSelection()
    {
        if (SelectedTournamentId is not { } id)
            return;

        var tournament = _tournaments.FirstOrDefault(t => t.Id == id);
        if (tournament?.PlayFormat is { } format)
            DefaultPlayFormat = format;
    }
}
