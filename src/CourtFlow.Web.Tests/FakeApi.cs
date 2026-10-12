using CourtFlow.Domain;
using CourtFlow.Web.Services;

namespace CourtFlow.Web.Tests;

public sealed record FakeTeam(Guid Id, Guid ClubId, string Name);

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

    /// <summary>When set, every division delete is rejected with this message.</summary>
    public string? RejectDivisionDeleteWith { get; set; }

    public List<ClubResponse> Clubs { get; } = [];
    public List<FakeTeam> Teams { get; } = [];
    public Dictionary<Guid, List<TournamentTeamResponse>> Entries { get; } = [];
    public int ListTeamsCalls { get; private set; }
    public int AddTeamCalls { get; private set; }
    public int UpdateTeamCalls { get; private set; }
    public int DeleteTeamCalls { get; private set; }
    public int PreviewCalls { get; private set; }
    public int ImportCalls { get; private set; }
    public TournamentTeamRequest? LastTeamAdded { get; private set; }
    public TournamentTeamUpdateRequest? LastTeamUpdated { get; private set; }

    /// <summary>When set, the next team add or update is rejected with this message, then clears.</summary>
    public string? RejectTeamSaveWith { get; set; }

    /// <summary>When set, <see cref="ListTournamentsAsync"/> waits on it before answering.</summary>
    public TaskCompletionSource? ListTournamentsGate { get; set; }

    /// <summary>When set, <see cref="ListTournamentTeamsAsync"/> waits on it before answering.</summary>
    public TaskCompletionSource? ListTeamsGate { get; set; }

    public TeamImportPreviewResponse? ImportPreview { get; set; }
    public TeamImportResponse? ImportResponse { get; set; }

    public async Task<IReadOnlyList<TournamentResponse>> ListTournamentsAsync(CancellationToken cancellationToken = default)
    {
        if (ListTournamentsGate is not null)
            await ListTournamentsGate.Task;

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
        if (RejectDivisionDeleteWith is { } rejection)
            throw new DomainRuleException(rejection);

        DeleteDivisionCalls++;
        foreach (var divisions in Divisions.Values)
            divisions.RemoveAll(division => division.Id == divisionId);
    }

    public async Task<IReadOnlyList<TournamentTeamResponse>> ListTournamentTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        if (ListTeamsGate is not null)
            await ListTeamsGate.Task;

        await ThrowIfFailing();
        ListTeamsCalls++;
        return EntriesFor(tournamentId).ToList();
    }

    public async Task<TournamentTeamResponse> AddTournamentTeamAsync(Guid tournamentId, TournamentTeamRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        RejectIfScripted();
        AddTeamCalls++;
        LastTeamAdded = request;

        var club = Clubs.FirstOrDefault(existing => Same(existing.Name, request.ClubName!));
        if (club is null)
        {
            club = new ClubResponse(Guid.NewGuid(), request.ClubName!.Trim());
            Clubs.Add(club);
        }

        var team = Teams.FirstOrDefault(existing => existing.ClubId == club.Id && Same(existing.Name, request.TeamName!));
        if (team is null)
        {
            team = new FakeTeam(Guid.NewGuid(), club.Id, request.TeamName!.Trim());
            Teams.Add(team);
        }

        var entries = EntriesFor(tournamentId);
        if (entries.Any(entry => entry.TeamId == team.Id))
            throw new DomainRuleException(Tournament.TeamAlreadyEntered);

        var entry = Entry(tournamentId, team, request.DivisionId!.Value);
        entries.Add(entry);
        return entry;
    }

    public async Task<TournamentTeamResponse> UpdateTournamentTeamAsync(Guid entryId, TournamentTeamUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        RejectIfScripted();
        UpdateTeamCalls++;
        LastTeamUpdated = request;

        foreach (var entries in Entries.Values)
        {
            var index = entries.FindIndex(entry => entry.Id == entryId);
            if (index < 0)
                continue;

            var existing = entries[index];
            var teamIndex = Teams.FindIndex(team => team.Id == existing.TeamId);
            Teams[teamIndex] = Teams[teamIndex] with { Name = request.TeamName!.Trim() };
            var updated = Entry(existing.TournamentId, Teams[teamIndex], request.DivisionId!.Value) with { Id = entryId };
            entries[index] = updated;
            return updated;
        }

        throw new InvalidOperationException("Entry was not found.");
    }

    public async Task DeleteTournamentTeamAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        DeleteTeamCalls++;
        foreach (var entries in Entries.Values)
            entries.RemoveAll(entry => entry.Id == entryId);
    }

    public async Task<IReadOnlyList<ClubResponse>> SearchClubsAsync(string? search, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        var key = search?.Trim() ?? "";
        return Clubs
            .Where(club => club.Name.Contains(key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(club => club.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<ClubTeamOptionResponse>> ListClubTeamsAsync(Guid clubId, Guid tournamentId, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        var entered = EntriesFor(tournamentId).Select(entry => entry.TeamId).ToHashSet();
        return Teams
            .Where(team => team.ClubId == clubId)
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Select(team => new ClubTeamOptionResponse(team.Id, team.Name, entered.Contains(team.Id)))
            .ToList();
    }

    public async Task<TeamImportPreviewResponse> PreviewTeamImportAsync(Guid tournamentId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        PreviewCalls++;
        return ImportPreview ?? throw new InvalidOperationException("No import preview is scripted.");
    }

    public async Task<TeamImportResponse> ImportTeamsAsync(Guid tournamentId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        await ThrowIfFailing();
        ImportCalls++;
        return ImportResponse ?? throw new InvalidOperationException("No import response is scripted.");
    }

    /// <summary>Adds an entry for a new or existing club and team, without counting it as a call.</summary>
    public TournamentTeamResponse SeedEntry(Guid tournamentId, string clubName, string teamName, Guid divisionId)
    {
        var club = Clubs.FirstOrDefault(existing => existing.Name == clubName);
        if (club is null)
        {
            club = new ClubResponse(Guid.NewGuid(), clubName);
            Clubs.Add(club);
        }

        var team = Teams.FirstOrDefault(existing => existing.ClubId == club.Id && existing.Name == teamName);
        if (team is null)
        {
            team = new FakeTeam(Guid.NewGuid(), club.Id, teamName);
            Teams.Add(team);
        }

        var entry = Entry(tournamentId, team, divisionId);
        EntriesFor(tournamentId).Add(entry);
        return entry;
    }

    private List<TournamentTeamResponse> EntriesFor(Guid tournamentId)
    {
        if (!Entries.TryGetValue(tournamentId, out var entries))
            Entries[tournamentId] = entries = [];
        return entries;
    }

    private TournamentTeamResponse Entry(Guid tournamentId, FakeTeam team, Guid divisionId)
    {
        var club = Clubs.Single(existing => existing.Id == team.ClubId);
        var division = Divisions.Values.SelectMany(divisions => divisions).Single(existing => existing.Id == divisionId);
        return new TournamentTeamResponse(
            Guid.NewGuid(),
            tournamentId,
            team.Id,
            team.Name,
            club.Id,
            club.Name,
            division.Id,
            division.Name,
            division.Gender);
    }

    private void RejectIfScripted()
    {
        if (RejectTeamSaveWith is not { } rejection)
            return;

        RejectTeamSaveWith = null;
        throw new DomainRuleException(rejection);
    }

    private static bool Same(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private Task ThrowIfFailing()
    {
        if (FailWith is null)
            return Task.CompletedTask;

        var message = FailWith;
        FailWith = null;
        throw new HttpRequestException(message);
    }
}
