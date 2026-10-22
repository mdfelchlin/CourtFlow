namespace CourtFlow.Api.Access;

/// <summary>
/// A call that changes data and carries the session cookie must come from a CourtFlow web origin. SameSite=Lax already
/// stops most cross-site posts; this closes the rest, such as a sibling subdomain.
/// </summary>
public sealed class SameOriginWrites(RequestDelegate next, IConfiguration configuration)
{
    private readonly HashSet<string> _origins = new(
        configuration.GetSection("Cors:Origins").Get<string[]>() ?? [],
        StringComparer.OrdinalIgnoreCase);

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
        if (safe || !request.Cookies.ContainsKey(SessionCookie.Name) || _origins.Contains(request.Headers.Origin.ToString()))
        {
            await next(context);
            return;
        }

        await AccessResultHandler.Forbid(context, AccessReasons.Forbidden);
    }
}