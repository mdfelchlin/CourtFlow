using CourtFlow.Web;
using CourtFlow.Web.Services;
using CourtFlow.Web.Theming;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5009/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<ICourtFlowApi, CourtFlowApi>();
builder.Services.AddMudServices();

builder.Services.AddSingleton<IThemePreferenceStore, JsThemePreferenceStore>();
builder.Services.AddSingleton<ThemeState>();
builder.Services.AddScoped<Feedback>();
builder.Services.AddScoped<Confirmations>();
builder.Services.AddScoped<ITournamentMemory, JsTournamentMemory>();

var host = builder.Build();

// Resolve the theme mode before the first render, so the frame's first paint already uses it.
await host.Services.GetRequiredService<ThemeState>().InitializeAsync();

await host.RunAsync();
