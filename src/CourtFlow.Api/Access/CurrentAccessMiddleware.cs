using CourtFlow.Application.Access;
using CourtFlow.Domain;
using CourtFlow.Infrastructure.Access;

namespace CourtFlow.Api.Access;

/// <summary>Runs after authentication: loads the caller once, then points the organization filters at their organization.</summary>
public sealed class CurrentAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentAccess access, DataFilter filter)
    {
        await access.LoadAsync(context.User, context.RequestAborted);
        if (access.ActiveRole() is { } role)
        {
            filter.OrganizationId = access.Membership!.OrganizationId;
            filter.PublishedOnly = role == OrganizationRole.Viewer;
        }

        await next(context);
    }
}