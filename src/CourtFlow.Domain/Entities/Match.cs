using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class Match : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid DivisionId { get; private set; }
    public Guid? PoolId { get; private set; }
    public Guid? BracketNodeId { get; private set; }
    public Guid? CourtId { get; private set; }
    public Guid? HomeTeamId { get; private set; }
    public Guid? AwayTeamId { get; private set; }
    public Guid? RefTeamId { get; private set; }
    public Wave Wave { get; private set; } = Wave.AM;
    public DateTime ScheduledStartUtc { get; private set; }
    public int DurationMinutes { get; private set; } = 60;
    public DateTime? ActualStartUtc { get; private set; }
    public DateTime? LastScoreAtUtc { get; private set; }
    public DateTime? FinalizedAtUtc { get; private set; }
    public MatchState State { get; private set; } = MatchState.NotStarted;
    public Guid? WinnerTeamId { get; private set; }

    private readonly List<MatchSet> _sets = new();
    public IReadOnlyCollection<MatchSet> Sets => _sets.AsReadOnly();

    private Match() { }

    public Match(
        Guid organizationId,
        Guid tournamentId,
        Guid divisionId,
        Wave wave,
        DateTime scheduledStartUtc,
        int durationMinutes,
        Guid? poolId = null,
        Guid? bracketNodeId = null,
        Guid? courtId = null,
        Guid? homeTeamId = null,
        Guid? awayTeamId = null,
        Guid? refTeamId = null)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        DivisionId = divisionId;
        Wave = wave;
        ScheduledStartUtc = scheduledStartUtc;
        DurationMinutes = durationMinutes;
        PoolId = poolId;
        BracketNodeId = bracketNodeId;
        CourtId = courtId;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        RefTeamId = refTeamId;
    }

    public DateTime ScheduledEndUtc => ScheduledStartUtc.AddMinutes(DurationMinutes);

    public void AssignCourt(Guid? courtId) { CourtId = courtId; Touch(); }
    public void Reschedule(DateTime scheduledStartUtc, int? durationMinutes = null)
    {
        ScheduledStartUtc = scheduledStartUtc;
        if (durationMinutes is not null) DurationMinutes = durationMinutes.Value;
        Touch();
    }
    public void AssignTeams(Guid? home, Guid? away) { HomeTeamId = home; AwayTeamId = away; Touch(); }
    public void AssignReferee(Guid? refTeamId) { RefTeamId = refTeamId; Touch(); }
    public void SetWave(Wave wave) { Wave = wave; Touch(); }

    public MatchSet ReportSet(int setNumber, int homeScore, int awayScore)
    {
        if (State == MatchState.Final || State == MatchState.Canceled)
            throw new InvalidOperationException($"Cannot update set scores when match state is {State}");

        var existing = _sets.FirstOrDefault(s => s.SetNumber == setNumber);
        if (existing is null)
        {
            existing = new MatchSet(OrganizationId, Id, setNumber, homeScore, awayScore);
            _sets.Add(existing);
        }
        else
        {
            existing.UpdateScores(homeScore, awayScore);
        }

        if (State == MatchState.NotStarted)
        {
            State = MatchState.InProgress;
            ActualStartUtc ??= DateTime.UtcNow;
        }
        LastScoreAtUtc = DateTime.UtcNow;
        Touch();
        return existing;
    }

    public void Finalize(Guid? winnerTeamId)
    {
        State = MatchState.Final;
        WinnerTeamId = winnerTeamId;
        FinalizedAtUtc = DateTime.UtcNow;
        LastScoreAtUtc ??= DateTime.UtcNow;
        ActualStartUtc ??= DateTime.UtcNow;
        Touch();
    }

    /// <summary>Declare a winner without set scores (forfeit).</summary>
    public void ForfeitTo(Guid winningTeamId)
    {
        if (State == MatchState.Final || State == MatchState.Canceled)
            throw new InvalidOperationException($"Cannot forfeit match in state {State}.");
        if (HomeTeamId != winningTeamId && AwayTeamId != winningTeamId)
            throw new ArgumentException("Winning team must be the home or away side.");

        Finalize(winningTeamId);
    }

    public void Cancel()
    {
        State = MatchState.Canceled;
        Touch();
    }
}
