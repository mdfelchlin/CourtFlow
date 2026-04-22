using CourtFlow.ApiClient;
using CourtFlow.Web.Client.Authentication;
using CourtFlow.Web.Shared.Services;
using CourtFlow.Web.Shared.Theming;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

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
