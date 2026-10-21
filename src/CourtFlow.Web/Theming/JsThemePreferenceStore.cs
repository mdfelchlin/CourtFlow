using Microsoft.JSInterop;

namespace CourtFlow.Web.Theming;

/// <summary>Reads and writes one named theme cookie through <c>wwwroot/js/theme.js</c>.</summary>
public sealed class JsThemePreferenceStore(IJSRuntime js, string cookieName) : IThemePreferenceStore
{
    public async Task<ThemePreference> ReadAsync() =>
        await js.InvokeAsync<ThemePreference>("courtflowTheme.read", cookieName);

    public async Task WriteAsync(ThemeMode mode) =>
        await js.InvokeVoidAsync("courtflowTheme.write", cookieName, mode.ToString().ToLowerInvariant());
}