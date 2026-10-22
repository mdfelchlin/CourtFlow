using CourtFlow.Api.Access;
using CourtFlow.Application.Access;
using CourtFlow.Application.Organizations;

namespace CourtFlow.Api;

/// <summary>The owner's Organization page: members and pending invitations of their own organization.</summary>
public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/organization").RequireAuthorization(AccessPolicies.Owner);

        group.MapGet("/", async (MemberUseCases useCases, CancellationToken cancellationToken) =>
            Results.Ok(await useCases.GetAsync(cancellationToken)));

        group.MapPost("/invitations", async (InviteMember body, MemberUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
            {
                await useCases.InviteAsync(body, cancellationToken);
                return Results.NoContent();
            }));

        group.MapPost("/invitations/{id:guid}/resend", async (Guid id, MemberUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.ResendAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound()));

        group.MapDelete("/invitations/{id:guid}", async (Guid id, MemberUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.CancelAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound()));

        group.MapPut("/members/{id:guid}/role", async (Guid id, ChangeMemberRole body, MemberUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.ChangeRoleAsync(id, body, cancellationToken) ? Results.NoContent() : Results.NotFound()));

        group.MapDelete("/members/{id:guid}", async (Guid id, MemberUseCases useCases, CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.RemoveAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound()));
    }
}