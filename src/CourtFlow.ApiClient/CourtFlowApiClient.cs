using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CourtFlow.ApiClient;

public class CourtFlowApiClient
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public CourtFlowApiClient(HttpClient http)
    {
        Tournaments = new TournamentEndpoints(http);
        Venues = new VenueEndpoints(http);
        Courts = new CourtEndpoints(http);
        Divisions = new DivisionEndpoints(http);
        Pools = new PoolEndpoints(http);
        Teams = new TeamEndpoints(http);
        Imports = new ImportEndpoints(http);
        Matches = new MatchEndpoints(http);
        Brackets = new BracketEndpoints(http);
        Schedule = new ScheduleEndpoints(http);
        CourtStatus = new CourtStatusEndpoints(http);
        Standings = new StandingsEndpoints(http);
        PublicEvents = new PublicEventEndpoints(http);
    }

    public TournamentEndpoints Tournaments { get; }
    public VenueEndpoints Venues { get; }
    public CourtEndpoints Courts { get; }
    public DivisionEndpoints Divisions { get; }
    public PoolEndpoints Pools { get; }
    public TeamEndpoints Teams { get; }
    public ImportEndpoints Imports { get; }
    public MatchEndpoints Matches { get; }
    public BracketEndpoints Brackets { get; }
    public ScheduleEndpoints Schedule { get; }
    public CourtStatusEndpoints CourtStatus { get; }
    public StandingsEndpoints Standings { get; }
    public PublicEventEndpoints PublicEvents { get; }
}
