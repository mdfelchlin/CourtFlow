using CourtFlow.Api.Access;
using CourtFlow.Application;
using CourtFlow.Application.Access;
using CourtFlow.Application.Tournaments;
using CourtFlow.Domain;

namespace CourtFlow.Api;

public static class TournamentEndpoints
{
    public static void MapTournamentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tournaments").RequireAuthorization(AccessPolicies.Member);

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
            })).RequireAuthorization(AccessPolicies.Editor);

        group.MapPut("/{id:guid}", async (Guid id, SaveTournament body, TournamentUseCases useCases, CancellationToken cancellationToken) =>
            await RejectRules(async () =>
                await useCases.UpdateAsync(id, body, cancellationToken) is { } tournament
                    ? Results.Ok(tournament)
                    : Results.NotFound())).RequireAuthorization(AccessPolicies.Editor);

        group.MapDelete("/{id:guid}", async (Guid id, TournamentUseCases useCases, CancellationToken cancellationToken) =>
            await RejectRules(async () =>
                await useCases.DeleteAsync(id, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound())).RequireAuthorization(AccessPolicies.Editor);

        MapTransition(group, "publish", (useCases, id, token) => useCases.PublishAsync(id, token));
        MapTransition(group, "unpublish", (useCases, id, token) => useCases.UnpublishAsync(id, token));
        MapTransition(group, "archive", (useCases, id, token) => useCases.ArchiveAsync(id, token));
        MapTransition(group, "restore", (useCases, id, token) => useCases.RestoreAsync(id, token));
    }

    private static void MapTransition(
        RouteGroupBuilder group,
        string action,
        Func<TournamentUseCases, Guid, CancellationToken, Task<TournamentDetails?>> transition) =>
        group.MapPost($"/{{id:guid}}/{action}", async (Guid id, TournamentUseCases useCases, CancellationToken cancellationToken) =>
            await RejectRules(async () =>
                await transition(useCases, id, cancellationToken) is { } tournament
                    ? Results.Ok(tournament)
                    : Results.NotFound())).RequireAuthorization(AccessPolicies.Editor);

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
        catch (WorkOsUnavailableException)
        {
            return Results.Json(new { error = SignInServiceUnavailable }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public const string SignInServiceUnavailable = "We couldn't reach the sign-in service. Please try again in a few minutes.";
}