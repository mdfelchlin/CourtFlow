using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;

namespace CourtFlow.ApiClient;

public static class CourtFlowSerialization
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly RefitSettings RefitSettings = new(new SystemTextJsonContentSerializer(JsonOptions));
}
