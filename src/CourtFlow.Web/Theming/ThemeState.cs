using Microsoft.JSInterop;

namespace CourtFlow.Web.Theming;

/// <summary>
/// The current light or dark mode. Registered as a singleton so <c>Program.cs</c> can
/// initialize the same instance the layout reads before the first render.
/// </summary>
public sealed class ThemeState(IThemePreferenceStore store)
{
    public ThemeMode Mode { get; private set; } = ThemeMode.Light;

    public bool IsDarkMode => Mode == ThemeMode.Dark;

    public event Action? Changed;

    /// <summary>Reads the cookie and the operating system setting once. Never writes.</summary>
    public async Task InitializeAsync()
    {
        var preference = await store.ReadAsync();
        Mode = Resolve(preference.Cookie, preference.SystemPrefersDark);
    }

    /// <summary>
    /// Flips the mode for this visit, then remembers it in the cookie. A browser that
    /// refuses the cookie still keeps the new mode until the page is reloaded.
    /// </summary>
    public async Task ToggleAsync()
    {
        Mode = IsDarkMode ? ThemeMode.Light : ThemeMode.Dark;
        Changed?.Invoke();

        try
        {
            await store.WriteAsync(Mode);
        }
        catch (JSException)
        {
            // The cookie was refused. The mode above still applies for the rest of the visit.
        }
    }

    /// <summary>A cookie of <c>light</c> or <c>dark</c> wins, ignoring case. Anything else follows the operating system.</summary>
    public static ThemeMode Resolve(string? cookieValue, bool systemPrefersDark)
    {
        if (string.Equals(cookieValue, "dark", StringComparison.OrdinalIgnoreCase))
            return ThemeMode.Dark;

        if (string.Equals(cookieValue, "light", StringComparison.OrdinalIgnoreCase))
            return ThemeMode.Light;

        return systemPrefersDark ? ThemeMode.Dark : ThemeMode.Light;
    }
}
