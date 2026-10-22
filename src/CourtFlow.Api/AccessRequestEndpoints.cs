using CourtFlow.Api.Access;
using CourtFlow.Application.Access;

namespace CourtFlow.Api;

public static class AccessRequestEndpoints
{
    public static void MapAccessRequestEndpoints(this WebApplication app)
    {
        // Every outcome, including a failed CAPTCHA, is an answer the form shows, so all of them are 200.
        app.MapPost("/api/access-requests", async (SubmitAccessRequest body, AccessRequestUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () => Results.Ok(await useCases.SubmitAsync(body, cancellationToken))))
            .AllowAnonymous();

        var group = app.MapGroup("/api/operator/requests").RequireAuthorization(AccessPolicies.Operator);

        group.MapGet("/", async (AccessRequestUseCases useCases, CancellationToken cancellationToken) =>
            Results.Ok(await useCases.ListWaitingAsync(cancellationToken)));

        group.MapPost("/{id:guid}/approve", async (Guid id, ApproveAccessRequest body, AccessRequestUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.ApproveAsync(id, body, cancellationToken) ? Results.NoContent() : Results.NotFound()));

        group.MapPost("/{id:guid}/decline", async (Guid id, AccessRequestUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.DeclineAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound()));
    }
}