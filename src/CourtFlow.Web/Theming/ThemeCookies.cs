namespace CourtFlow.Web.Theming;

/// <summary>
/// The cookie each frame remembers its light or dark choice in. The two are independent, so the
/// landing page can be dark while the director app is light.
/// </summary>
public static class ThemeCookies
{
    public const string Director = "courtflow-theme";
    public const string Public = "courtflow-public-theme";
}