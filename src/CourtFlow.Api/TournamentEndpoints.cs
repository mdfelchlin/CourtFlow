using CourtFlow.Application;
using CourtFlow.Application.Tournaments;
using CourtFlow.Domain;

namespace CourtFlow.Api;

public static class TournamentEndpoints
{
    public static void MapTournamentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tournaments");

        group.MapGet("/", async (TournamentUseCases useCases, CancellationToken cancellationToken) =>
            Results.Ok(await useCases.ListAsync(cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, TournamentUseCases useCases, CancellationToken cancellationToken) =>
            await useCases.GetAsync(id, cancellationToken) is { } tournament
                ? Results.Ok(tournament)
                : Results.NotFound());

        group.MapPost("/", async (SaveTournament body, TournamentUseCases useCases, CancellationToken cancellationToken) =>
            await RejectRules(async () =>
            {
                var created = await useCases.CreateAsync(body, cancellationToken);
                return Results.Created($"/api/tournaments/{created.Id}", created);
            }));

        group.MapPut("/{id:guid}", async (Guid id, SaveTournament body, TournamentUseCases useCases, CancellationToken cancellationToken) =>
            await RejectRules(async () =>
                await useCases.UpdateAsync(id, body, cancellationToken) is { } tournament
                    ? Results.Ok(tournament)
                    : Results.NotFound()));

        group.MapDelete("/{id:guid}", async (Guid id, TournamentUseCases useCases, CancellationToken cancellationToken) =>
            await useCases.DeleteAsync(id, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());
    }

    internal static async Task<IResult> RejectRules(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainRuleException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
