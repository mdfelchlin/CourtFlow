using System.Security.Claims;

using Bunit;
using Bunit.TestDoubles;

using CourtFlow.Domain;
using CourtFlow.Web.Services;

using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Web.Tests;

/// <summary>Who is signed in, for bUnit. The claims and policies come from the app's own <see cref="AccessClaims"/>.</summary>
public static class TestAccess
{
    public const string ApiBase = "https://api.courtflow.test/";
    public const string OrganizationName = "Test Organization";

    public static MeResponse Member(OrganizationRole role, bool isOperator = false, OrganizationStatus status = OrganizationStatus.Active) =>
        new(true, "Riley Rivera", "riley@club.test", isOperator, new MeOrganization(Guid.NewGuid(), OrganizationName, status, role));

    public static MeResponse NoMembership(bool isOperator = false) =>
        new(true, "Nora Newcomer", "nora@club.test", isOperator, null);

    public static readonly MeResponse SignedOut = new(false, null, null, false, null);

    /// <summary>Registers what the access components inject: the sign-in links and the Turnstile key.</summary>
    public static void AddAccessServices(this BunitContext context)
    {
        context.Services.AddSingleton(new SignInLinks(ApiBase));
        context.Services.AddSingleton(new TurnstileSettings("test-site-key"));
    }

    public static BunitAuthorizationContext SignIn(this BunitContext context, MeResponse me)
    {
        var authorization = context.AddAuthorization();
        if (!me.SignedIn)
            return authorization;

        var principal = AccessClaims.Principal(me);
        authorization.SetAuthorized(principal.Identity!.Name!);
        authorization.SetClaims(AccessClaims.For(me).Where(claim => claim.Type != ClaimTypes.Name).ToArray());
        authorization.SetPolicies(AccessPolicies.Granted(principal).ToArray());
        return authorization;
    }

    /// <summary>MudBlazor's activator answers pointer events bUnit cannot raise, so the menu is opened directly.</summary>
    public static void OpenUserMenu(this IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut) =>
        cut.InvokeAsync(() => cut.FindComponent<MudBlazor.MudMenu>().Instance.OpenMenuAsync(EventArgs.Empty)).GetAwaiter().GetResult();

    public static string Login(string returnUrl) => $"{ApiBase}auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

    public const string Logout = ApiBase + "auth/logout";
}