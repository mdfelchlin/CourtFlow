using CourtFlow.Api.Access;
using CourtFlow.Application.Access;
using CourtFlow.Application.Organizations;

namespace CourtFlow.Api;

public static class OperatorOrganizationEndpoints
{
    public static void MapOperatorOrganizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/operator/organizations").RequireAuthorization(AccessPolicies.Operator);

        group.MapGet("/", async (OperatorOrganizationUseCases useCases, CancellationToken cancellationToken) =>
            Results.Ok(await useCases.ListAsync(cancellationToken)));

        group.MapPost("/{id:guid}/owner-invitations", async (Guid id, InviteOwner body, OperatorOrganizationUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.InviteOwnerAsync(id, body, cancellationToken) ? Results.NoContent() : Results.NotFound()));

        group.MapPost("/{id:guid}/deactivate", async (Guid id, OperatorOrganizationUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.DeactivateAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound()));

        group.MapPost("/{id:guid}/reactivate", async (Guid id, OperatorOrganizationUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.ReactivateAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound()));
    }
}