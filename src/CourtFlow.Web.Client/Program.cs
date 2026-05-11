using CourtFlow.ApiClient;
using CourtFlow.Web.Client.Authentication;
using CourtFlow.Web.Shared.Options;
using CourtFlow.Web.Shared.Services;
using CourtFlow.Web.Shared.Theming;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using System.Net.Http;
using System.Text;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

try
{
    using var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    var json = await http.GetStringAsync("appsettings.json");
    if (!string.IsNullOrWhiteSpace(json))
        builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
}
catch
{
    // Embedded config only.
}

builder.Services.AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();

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

builder.Services.AddHttpClient(CourtFlowApiDependencyInjection.HttpClientName)
    .ConfigureHttpClient((sp, client) =>
    {
        var api = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
        client.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + '/');
    })
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddCourtFlowRefitApiClients();

await builder.Build().RunAsync();
