using CourtFlow.Web;
using CourtFlow.Web.Services;
using CourtFlow.Web.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API must be called over https: the session cookie is SameSite=Lax, and Chrome treats http and https as different sites.
var apiBase = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7011/");
builder.Services.AddSingleton(new SignInLinks(apiBase.ToString()));
builder.Services.AddSingleton(new TurnstileSettings(builder.Configuration["Turnstile:SiteKey"] ?? ""));
builder.Services.AddScoped(_ => new MeAuthenticationStateProvider(
    new HttpClient(new IncludeCredentialsHandler { InnerHandler = new HttpClientHandler() }) { BaseAddress = apiBase }));
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<MeAuthenticationStateProvider>());
builder.Services.AddScoped(services => new HttpClient(new AccessStatusHandler(
    services.GetRequiredService<NavigationManager>(),
    services.GetRequiredService<SignInLinks>(),
    services.GetRequiredService<MeAuthenticationStateProvider>())
{
    InnerHandler = new IncludeCredentialsHandler { InnerHandler = new HttpClientHandler() },
})
{
    BaseAddress = apiBase,
});
builder.Services.AddAuthorizationCore(AccessPolicies.Add);
builder.Services.AddScoped<ICourtFlowApi, CourtFlowApi>();
builder.Services.AddMudServices();

// Each frame gets its own state over its own cookie, so the two choices never overwrite each other.
builder.Services.AddSingleton(services =>
    new ThemeState(new JsThemePreferenceStore(services.GetRequiredService<IJSRuntime>(), ThemeCookies.Director)));
builder.Services.AddSingleton(services =>
    new PublicThemeState(new JsThemePreferenceStore(services.GetRequiredService<IJSRuntime>(), ThemeCookies.Public)));
builder.Services.AddScoped<Feedback>();
builder.Services.AddScoped<Confirmations>();
builder.Services.AddScoped<ITournamentMemory, JsTournamentMemory>();
builder.Services.AddSingleton<IRankingRandom, SystemRankingRandom>();

var host = builder.Build();

// Resolve both theme modes before the first render, so each frame's first paint already uses its own.
await host.Services.GetRequiredService<ThemeState>().InitializeAsync();
await host.Services.GetRequiredService<PublicThemeState>().InitializeAsync();

// Who is signed in is known before the first render too, so no page loads organization data for a stranger.
await host.Services.GetRequiredService<MeAuthenticationStateProvider>().GetAuthenticationStateAsync();

await host.RunAsync();
