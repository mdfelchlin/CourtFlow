using CourtFlow.ApiClient;
using CourtFlow.Web.Components;
using CourtFlow.Web.Shared.Options;
using CourtFlow.Web.Shared.Services;
using CourtFlow.Web.Shared.Theming;
using CourtFlow.ServiceDefaults;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

        builder.AddCourtFlowServiceDefaults();

        builder.Services.AddOptions<ApiOptions>()
            .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();

        builder.Services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents();

        builder.Services.AddMudServices();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ThemeState>();
        builder.Services.AddScoped<SelectedTournamentStore>();

        builder.Services.AddHttpClient(CourtFlowApiDependencyInjection.HttpClientName)
            .ConfigureHttpClient((sp, client) =>
            {
                var api = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
                client.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + '/');
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var handler = new HttpClientHandler();
                var env = sp.GetRequiredService<IHostEnvironment>();
                if (env.IsDevelopment())
                    handler.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                return handler;
            });
        builder.Services.AddCourtFlowRefitApiClients();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.MapCourtFlowHealthChecks();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/_framework"))
                {
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                        context.Response.Headers.Pragma = "no-cache";
                        return Task.CompletedTask;
                    });
                }

                await next();
            });
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseAntiforgery();

        app.MapStaticAssets();

        // Interactive auth runs in WebAssembly (Clerk + JWT). Do not require an authenticated server principal
        // for component routes — that would Challenge and require IAuthenticationService / server cookies.
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(CourtFlow.Web.Client._Imports).Assembly,
                typeof(CourtFlow.Web.Shared.Components.AppShell).Assembly)
            .AllowAnonymous();

        app.Run();
    }
}
