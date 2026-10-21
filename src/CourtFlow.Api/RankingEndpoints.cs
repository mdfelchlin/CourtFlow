using CourtFlow.Api.Access;
using CourtFlow.Application;
using CourtFlow.Application.Rankings;

namespace CourtFlow.Api;

public static class RankingEndpoints
{
    public static void MapRankingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tournaments/{tournamentId:guid}/format", async (
            Guid tournamentId,
            RankingUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.ListAsync(tournamentId, cancellationToken) is { } divisions
                ? Results.Ok(divisions)
                : Results.NotFound());

        app.MapGet("/api/divisions/{divisionId:guid}/ranking", async (
            Guid divisionId,
            RankingUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.GetAsync(divisionId, cancellationToken) is { } ranking
                ? Results.Ok(ranking)
                : Results.NotFound());

        app.MapPut("/api/divisions/{divisionId:guid}/ranking", async (
            Guid divisionId,
            SaveRanking body,
            RankingUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.SaveAsync(divisionId, body, cancellationToken) is { } ranking
                    ? Results.Ok(ranking)
                    : Results.NotFound())).RequireAuthorization(AccessPolicies.Editor);
    }
}
