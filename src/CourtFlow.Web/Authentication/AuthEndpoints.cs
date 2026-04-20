using System.Security.Claims;
using CourtFlow.ApiClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Web.Authentication;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login", async (
            HttpContext http,
            [FromServices] CourtFlowApiClient api,
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] string? returnUrl) =>
        {
            try
            {
                var signIn = await api.Auth.SignInAsync(new SignInRequestDto(email, password, null));

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, signIn.UserId.ToString()),
                    new(ClaimTypes.Name, email),
                    new("org_id", signIn.OrganizationId.ToString()),
                    new("access_token", signIn.Token),
                    new("access_token_expires", signIn.ExpiresUtc.ToString("O"))
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = signIn.ExpiresUtc
                });

                var target = string.IsNullOrWhiteSpace(returnUrl) ? "/tournaments" : returnUrl;
                return Results.Redirect(target);
            }
            catch (Exception)
            {
                return Results.Redirect("/login?error=Invalid+credentials");
            }
        }).DisableAntiforgery();

        endpoints.MapGet("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        });

        return endpoints;
    }
}
