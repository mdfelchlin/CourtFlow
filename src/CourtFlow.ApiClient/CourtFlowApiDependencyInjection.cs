using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace CourtFlow.ApiClient;

public static class CourtFlowApiDependencyInjection
{
    public const string HttpClientName = "CourtFlowBackend";

    public static void AddCourtFlowRefitApiClients(this IServiceCollection services)
    {
        services.AddCourtFlowTypedClient<ICourtFlowTournamentsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowVenuesApi>();
        services.AddCourtFlowTypedClient<ICourtFlowCourtsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowDivisionsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowPoolsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowTeamsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowImportsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowMatchesApi>();
        services.AddCourtFlowTypedClient<ICourtFlowBracketsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowScheduleApi>();
        services.AddCourtFlowTypedClient<ICourtFlowScheduleVersionsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowCourtStatusApi>();
        services.AddCourtFlowTypedClient<ICourtFlowStandingsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowPublicEventsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowRepairsApi>();
        services.AddCourtFlowTypedClient<ICourtFlowTemplatesApi>();

        services.AddTransient<CourtFlowApiClient>();
    }

    private static void AddCourtFlowTypedClient<TApi>(this IServiceCollection services) where TApi : class =>
        services.AddTransient(sp =>
            RestService.For<TApi>(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
                CourtFlowSerialization.RefitSettings));
}
