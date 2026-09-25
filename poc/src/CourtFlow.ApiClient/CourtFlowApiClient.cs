using System.Text.Json;

namespace CourtFlow.ApiClient;

public sealed class CourtFlowApiClient(
    ICourtFlowTournamentsApi tournaments,
    ICourtFlowVenuesApi venues,
    ICourtFlowCourtsApi courts,
    ICourtFlowDivisionsApi divisions,
    ICourtFlowWavesApi waves,
    ICourtFlowStagesApi stages,
    ICourtFlowPoolsApi pools,
    ICourtFlowTeamsApi teams,
    ICourtFlowImportsApi imports,
    ICourtFlowMatchesApi matches,
    ICourtFlowBracketsApi brackets,
    ICourtFlowScheduleApi schedule,
    ICourtFlowScheduleVersionsApi scheduleVersions,
    ICourtFlowCourtStatusApi courtStatus,
    ICourtFlowStandingsApi standings,
    ICourtFlowPublicEventsApi publicEvents,
    ICourtFlowRepairsApi repairs,
    ICourtFlowTemplatesApi templates)
{
    /// <summary>Compatible with legacy Blazor deserialization — same camelCase Web defaults.</summary>
    public static JsonSerializerOptions JsonOptions => CourtFlowSerialization.JsonOptions;

    public TournamentEndpoints Tournaments { get; } = new(tournaments);
    public VenueEndpoints Venues { get; } = new(venues);
    public CourtEndpoints Courts { get; } = new(courts);
    public DivisionEndpoints Divisions { get; } = new(divisions);
    public WaveEndpoints Waves { get; } = new(waves);
    public StageEndpoints Stages { get; } = new(stages);
    public PoolEndpoints Pools { get; } = new(pools);
    public TeamEndpoints Teams { get; } = new(teams);
    public ImportEndpoints Imports { get; } = new(imports);
    public MatchEndpoints Matches { get; } = new(matches);
    public BracketEndpoints Brackets { get; } = new(brackets);
    public ScheduleEndpoints Schedule { get; } = new(schedule);
    public ScheduleVersionsEndpoints ScheduleVersions { get; } = new(scheduleVersions);
    public CourtStatusEndpoints CourtStatus { get; } = new(courtStatus);
    public StandingsEndpoints Standings { get; } = new(standings);
    public PublicEventEndpoints PublicEvents { get; } = new(publicEvents);
    public RepairsEndpoints Repairs { get; } = new(repairs);
    public TemplatesEndpoints Templates { get; } = new(templates);
}
