namespace CourtFlow.Web.Theming;

/// <summary>
/// The public frame's light or dark choice. It is its own type so the public layout and the
/// director layout resolve different instances, each with its own cookie.
/// </summary>
public sealed class PublicThemeState(IThemePreferenceStore store) : ThemeState(store);
