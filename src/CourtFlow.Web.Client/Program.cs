using CourtFlow.ApiClient;
using CourtFlow.Web.Client.Authentication;
using CourtFlow.Web.Shared.Services;
using CourtFlow.Web.Shared.Theming;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using MudBlazor.Services;
using System.Net.Http;
using System.Text;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// On static hosts, IConfiguration from CreateDefault is sometimes empty. Merge from wwwroot.
// Blazor WebAssembly: do not use GetStreamAsync+AddJsonStream (HTTP response streams are async-only
// in the browser; AddJsonStream reads sync → net_http_synchronous_reads_not_supported).
try
{
    using var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    var json = await http.GetStringAsync("appsettings.json");
    if (!string.IsNullOrWhiteSpace(json))
    {
        builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
catch
{
    // Keep CreateDefault/embedded only.
}

builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<SelectedTournamentStore>();
builder.Services.AddScoped<AuthTokenStore>();
builder.Services.AddScoped<IClerkClientOptionsProvider, ClerkClientOptionsProvider>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<JwtAuthenticationStateProvider>();

var apiBase = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured");

builder.Services.AddHttpClient<CourtFlowApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBase);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

await builder.Build().RunAsync();
