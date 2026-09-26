namespace CourtFlow.Web.Theming;

/// <summary>What the browser knows about the director's theme: the cookie, if any, and the operating system setting.</summary>
public sealed record ThemePreference(string? Cookie, bool SystemPrefersDark);

public interface IThemePreferenceStore
{
    Task<ThemePreference> ReadAsync();

    Task WriteAsync(ThemeMode mode);
}
