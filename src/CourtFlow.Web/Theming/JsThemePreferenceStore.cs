using Microsoft.JSInterop;

namespace CourtFlow.Web.Theming;

/// <summary>Reads and writes the theme cookie through <c>wwwroot/js/theme.js</c>.</summary>
public sealed class JsThemePreferenceStore(IJSRuntime js) : IThemePreferenceStore
{
    public async Task<ThemePreference> ReadAsync() =>
        await js.InvokeAsync<ThemePreference>("courtflowTheme.read");

    public async Task WriteAsync(ThemeMode mode) =>
        await js.InvokeVoidAsync("courtflowTheme.write", mode.ToString().ToLowerInvariant());
}
