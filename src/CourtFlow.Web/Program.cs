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
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseAntiforgery();

        app.MapStaticAssets();

        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(CourtFlow.Web.Client._Imports).Assembly,
                typeof(CourtFlow.Web.Shared.Components.AppShell).Assembly);

        app.Run();
    }
}
