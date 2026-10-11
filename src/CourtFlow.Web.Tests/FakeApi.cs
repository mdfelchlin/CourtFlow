using CourtFlow.Web.Services;

namespace CourtFlow.Web.Tests;

public sealed class FakeApi : ICourtFlowApi
{
    public List<TournamentResponse> Tournaments { get; } = [];
    public Dictionary<Guid, List<VenueResponse>> Venues { get; } = [];
    public Dictionary<Guid, List<DivisionResponse>> Divisions { get; } = [];
    public int DeleteTournamentCalls { get; private set; }
    public int DeleteVenueCalls { get; private set; }
    public int DeleteDivisionCalls { get; private set; }
    public int CreateCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public int CreateVenueCalls { get; private set; }
    public int UpdateVenueCalls { get; private set; }
    public int CreateDivisionCalls { get; private set; }
    public int UpdateDivisionCalls { get; private set; }
    public TournamentRequest? LastSaved { get; private set; }
    public VenueRequest? LastVenueSaved { get; private set; }
    public DivisionRequest? LastDivisionSaved { get; private set; }

    /// <summary>When set, the next call throws <see cref="HttpRequestException"/> with this message, then clears.</summary>
    public string? FailWith { get; set; }

    /// <summary>When set, <see cref="GetTournamentAsync"/> waits on it before answering.</summary>
    public TaskCompletionSource? GetTournamentGate { get; set; }

    public async Task<IReadOnlyList<TournamentResponse>> ListTournamentsAsync(CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        return Tournaments;
    }

    public async Task<TournamentResponse?> GetTournamentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (GetTournamentGate is not null)
            await GetTournamentGate.Task;

        await ThrowIfFailing();
        return Tournaments.FirstOrDefault(tournament => tournament.Id == id);
    }

    public async Task<TournamentResponse> CreateTournamentAsync(TournamentRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        CreateCalls++;
        LastSaved = request;
        var created = new TournamentResponse(
            Guid.NewGuid(),
            request.Name!.Trim(),
            request.Description ?? "",
            request.StartDate!.Value,
            request.EndDate!.Value,
            request.TimeZoneId!,
            request.DefaultMatchDurationMinutes!.Value);
        Tournaments.Add(created);
        return created;
    }

    public async Task<TournamentResponse> UpdateTournamentAsync(Guid id, TournamentRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        UpdateCalls++;
        LastSaved = request;
        var existing = Tournaments.Single(tournament => tournament.Id == id);
        var updated = existing with
        {
            Name = request.Name!.Trim(),
            Description = request.Description ?? "",
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate!.Value,
            TimeZoneId = request.TimeZoneId!,
            DefaultMatchDurationMinutes = request.DefaultMatchDurationMinutes!.Value,
        };
        Tournaments[Tournaments.IndexOf(existing)] = updated;
        return updated;
    }

    public async Task DeleteTournamentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        DeleteTournamentCalls++;
        Tournaments.RemoveAll(tournament => tournament.Id == id);
    }

    public async Task<IReadOnlyList<VenueResponse>> ListVenuesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        return Venues.TryGetValue(tournamentId, out var venues) ? venues : [];
    }

    public async Task<VenueResponse> CreateVenueAsync(Guid tournamentId, VenueRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        CreateVenueCalls++;
        LastVenueSaved = request;
        var created = new VenueResponse(Guid.NewGuid(), tournamentId, request.Name!.Trim(), request.Address ?? "", request.CourtCount ?? 0, []);
        if (!Venues.TryGetValue(tournamentId, out var venues))
            Venues[tournamentId] = venues = [];
        venues.Add(created);
        return created;
    }

    public async Task<VenueResponse> UpdateVenueAsync(Guid venueId, VenueRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        UpdateVenueCalls++;
        LastVenueSaved = request;
        foreach (var venues in Venues.Values)
        {
            var index = venues.FindIndex(venue => venue.Id == venueId);
            if (index < 0)
                continue;

            var updated = venues[index] with
            {
                Name = request.Name!.Trim(),
                Address = request.Address ?? "",
                CourtCount = request.CourtCount ?? 0,
            };
            venues[index] = updated;
            return updated;
        }

        throw new InvalidOperationException("Venue was not found.");
    }

    public async Task DeleteVenueAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        DeleteVenueCalls++;
        foreach (var venues in Venues.Values)
            venues.RemoveAll(venue => venue.Id == venueId);
    }

    public async Task<IReadOnlyList<DivisionResponse>> ListDivisionsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        return Divisions.TryGetValue(tournamentId, out var divisions) ? divisions : [];
    }

    public async Task<DivisionResponse> CreateDivisionAsync(Guid tournamentId, DivisionRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        CreateDivisionCalls++;
        LastDivisionSaved = request;
        var created = new DivisionResponse(Guid.NewGuid(), tournamentId, request.Name!.Trim(), request.Gender!);
        if (!Divisions.TryGetValue(tournamentId, out var divisions))
            Divisions[tournamentId] = divisions = [];
        divisions.Add(created);
        return created;
    }

    public async Task<DivisionResponse> UpdateDivisionAsync(Guid divisionId, DivisionRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        UpdateDivisionCalls++;
        LastDivisionSaved = request;
        foreach (var divisions in Divisions.Values)
        {
            var index = divisions.FindIndex(division => division.Id == divisionId);
            if (index < 0)
                continue;

            var updated = divisions[index] with
            {
                Name = request.Name!.Trim(),
                Gender = request.Gender!,
            };
            divisions[index] = updated;
            return updated;
        }

        throw new InvalidOperationException("Division was not found.");
    }

    public async Task DeleteDivisionAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        DeleteDivisionCalls++;
        foreach (var divisions in Divisions.Values)
            divisions.RemoveAll(division => division.Id == divisionId);
    }

    private Task ThrowIfFailing()
    {
        if (FailWith is null)
            return Task.CompletedTask;

        var message = FailWith;
        FailWith = null;
        throw new HttpRequestException(message);
    }
}
