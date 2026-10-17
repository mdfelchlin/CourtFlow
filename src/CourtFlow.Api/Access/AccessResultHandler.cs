using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace CourtFlow.Api.Access;

/// <summary>An API answer instead of a redirect: <c>401</c> when signed out, otherwise <c>403</c> with the reason.</summary>
public sealed class AccessResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly string[] Reasons = [AccessReasons.NoAccess, AccessReasons.OrganizationDeactivated, AccessReasons.Forbidden];

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        if (authorizeResult.Challenged)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var reason = authorizeResult.AuthorizationFailure?.FailureReasons
            .Select(failure => failure.Message)
            .FirstOrDefault(Reasons.Contains) ?? AccessReasons.Forbidden;
        await Forbid(context, reason);
    }

    public static Task Forbid(HttpContext context, string reason)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new { reason });
    }
}
