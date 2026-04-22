using CourtFlow.ApiClient;
using CourtFlow.Web.Components;
using CourtFlow.Web.Shared.Services;
using CourtFlow.Web.Shared.Theming;
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
            .AddInteractiveWebAssemblyComponents();

        builder.Services.AddMudServices();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ThemeState>();
        builder.Services.AddScoped<SelectedTournamentStore>();

        var apiBase = builder.Configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Api:BaseUrl is not configured");

        builder.Services.AddHttpClient<CourtFlowApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBase);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (builder.Environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
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

        app.MapStaticAssets();

        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(CourtFlow.Web.Client._Imports).Assembly,
                typeof(CourtFlow.Web.Shared.Components.AppShell).Assembly);

        app.Run();
    }
}
