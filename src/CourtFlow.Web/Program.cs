using CourtFlow.ApiClient;
using CourtFlow.Web.Authentication;
using CourtFlow.Web.Components;
using CourtFlow.Web.Shared.Services;
using CourtFlow.Web.Shared.Theming;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using MudBlazor.Services;
using Serilog;

namespace CourtFlow.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((ctx, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/courtflow-web-.log", rollingInterval: RollingInterval.Day));

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddMudServices();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<ThemeState>();
        builder.Services.AddScoped<SelectedTournamentStore>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
            });

        builder.Services.AddAuthorization();

        builder.Services.AddTransient<BearerTokenHandler>();

        var apiBase = builder.Configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Api:BaseUrl is not configured");

        builder.Services.AddHttpClient<CourtFlowApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBase);
        })
        .AddHttpMessageHandler<BearerTokenHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseSerilogRequestLogging();
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseAntiforgery();

        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true
                && !context.Request.Path.StartsWithSegments("/login")
                && !context.Request.Path.StartsWithSegments("/logout")
                && !context.Request.Path.StartsWithSegments("/auth/login")
                && !context.Request.Path.StartsWithSegments("/_blazor"))
            {
                var expiresClaim = context.User.FindFirst("access_token_expires")?.Value;
                if (DateTimeOffset.TryParse(expiresClaim, out var accessTokenExpiresUtc)
                    && accessTokenExpiresUtc <= DateTimeOffset.UtcNow)
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
                    context.Response.Redirect($"/login?returnUrl={returnUrl}");
                    return;
                }
            }

            await next();
        });

        app.MapStaticAssets();
        app.MapAuthEndpoints();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(CourtFlow.Web.Shared.Components.AppShell).Assembly);

        app.Run();
    }
}
