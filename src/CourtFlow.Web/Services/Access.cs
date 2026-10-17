using System.Security.Claims;
using CourtFlow.Domain;
using Microsoft.AspNetCore.Authorization;

namespace CourtFlow.Web.Services;

/// <summary>The claims the app builds from <c>/api/me</c>. The API checks every call again; these only shape the screens.</summary>
public static class AccessClaims
{
    public const string AuthenticationType = "courtflow";
    public const string Operator = "courtflow:operator";
    public const string OrganizationName = "courtflow:organization_name";
    public const string OrganizationStatus = "courtflow:organization_status";

    public static IEnumerable<Claim> For(MeResponse me)
    {
        if (!me.SignedIn)
            yield break;

        yield return new Claim(ClaimTypes.Name, me.Name ?? me.Email ?? "");
        yield return new Claim(ClaimTypes.Email, me.Email ?? "");
        if (me.IsOperator)
            yield return new Claim(Operator, "true");
        if (me.Organization is { } organization)
        {
            yield return new Claim(OrganizationName, organization.Name);
            yield return new Claim(OrganizationStatus, organization.Status.ToString());
            yield return new Claim(ClaimTypes.Role, organization.Role.ToString());
        }
    }

    public static ClaimsPrincipal Principal(MeResponse me) =>
        me.SignedIn ? new ClaimsPrincipal(new ClaimsIdentity(For(me), AuthenticationType)) : new ClaimsPrincipal(new ClaimsIdentity());

    public static bool IsSignedIn(this ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool IsOperator(this ClaimsPrincipal user) => user.HasClaim(Operator, "true");

    public static string? OrganizationNameOf(this ClaimsPrincipal user) => user.FindFirst(OrganizationName)?.Value;

    public static bool IsDeactivated(this ClaimsPrincipal user) =>
        user.FindFirst(OrganizationStatus)?.Value == nameof(Domain.OrganizationStatus.Deactivated);

    /// <summary>The role in an active organization, or null with no membership or a deactivated one.</summary>
    public static OrganizationRole? ActiveRole(this ClaimsPrincipal user) =>
        user.FindFirst(OrganizationStatus)?.Value == nameof(Domain.OrganizationStatus.Active)
        && Enum.TryParse<OrganizationRole>(user.FindFirst(ClaimTypes.Role)?.Value, out var role)
            ? role
            : null;

    public static bool HasActiveMembership(this ClaimsPrincipal user) => user.ActiveRole() is not null;

    public static bool IsViewer(this ClaimsPrincipal user) => user.ActiveRole() == OrganizationRole.Viewer;

    public static bool CanEdit(this ClaimsPrincipal user) => user.ActiveRole() is { } role && OrganizationRoles.CanEdit(role);

    public static bool IsOwner(this ClaimsPrincipal user) => user.ActiveRole() == OrganizationRole.Owner;

    public static string Initials(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant(),
        };
    }
}

/// <summary>The same four policies the API enforces, read from the claims above.</summary>
public static class AccessPolicies
{
    public const string Member = "Member";
    public const string Editor = "Editor";
    public const string Owner = "Owner";
    public const string Operator = "Operator";

    private static readonly (string Name, Func<ClaimsPrincipal, bool> Allows)[] All =
    [
        (Member, user => user.HasActiveMembership()),
        (Editor, user => user.CanEdit()),
        (Owner, user => user.IsOwner()),
        (Operator, user => user.IsOperator()),
    ];

    public static void Add(AuthorizationOptions options)
    {
        foreach (var (name, allows) in All)
            options.AddPolicy(name, policy => policy.RequireAssertion(context => allows(context.User)));
    }

    /// <summary>The policies this person passes, for tests that fake the authorization service.</summary>
    public static IReadOnlyList<string> Granted(ClaimsPrincipal user) =>
        All.Where(policy => policy.Allows(user)).Select(policy => policy.Name).ToList();
}

/// <summary>Sign-in and sign-out are browser navigations to the API, which owns the session cookie.</summary>
public sealed class SignInLinks(string apiBaseUrl)
{
    private readonly string _base = apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/";

    public string Login(string returnUrl) => $"{_base}auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

    public string Logout() => $"{_base}auth/logout";
}
