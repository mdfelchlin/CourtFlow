using CourtFlow.Application.Access;

namespace CourtFlow.Api;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me", (ICurrentAccess access) =>
            Results.Ok(access.IsSignedIn
                ? new MeResponse(
                    true,
                    access.Name,
                    access.Email,
                    access.IsOperator,
                    access.Membership is { } membership
                        ? new MeOrganization(membership.OrganizationId, membership.OrganizationName, membership.OrganizationStatus, membership.Role)
                        : null)
                : new MeResponse(false, null, null, false, null)))
            .AllowAnonymous();
    }
}