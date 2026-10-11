using CourtFlow.Application;
using CourtFlow.Application.Divisions;

namespace CourtFlow.Api;

public static class DivisionEndpoints
{
    public static void MapDivisionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tournaments/{tournamentId:guid}/divisions", async (
            Guid tournamentId,
            DivisionUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.ListAsync(tournamentId, cancellationToken) is { } divisions
                ? Results.Ok(divisions)
                : Results.NotFound());

        app.MapPost("/api/tournaments/{tournamentId:guid}/divisions", async (
            Guid tournamentId,
            SaveDivision body,
            DivisionUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.CreateAsync(tournamentId, body, cancellationToken) is { } division
                    ? Results.Created($"/api/divisions/{division.Id}", division)
                    : Results.NotFound()));

        app.MapPut("/api/divisions/{divisionId:guid}", async (
            Guid divisionId,
            SaveDivision body,
            DivisionUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.UpdateAsync(divisionId, body, cancellationToken) is { } division
                    ? Results.Ok(division)
                    : Results.NotFound()));

        app.MapDelete("/api/divisions/{divisionId:guid}", async (
            Guid divisionId,
            DivisionUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.DeleteAsync(divisionId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());
    }
}
