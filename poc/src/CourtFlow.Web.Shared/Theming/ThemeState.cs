using Microsoft.AspNetCore.Http;
using MudBlazor;

namespace CourtFlow.Web.Shared.Theming;

/// <summary>
/// Scoped holder for the active MudBlazor theme plus dark/light preference. On the
/// initial SSR request we read the <c>courtflow-theme</c> cookie via <see cref="IHttpContextAccessor"/>
/// so the first render already matches the user's preference. Subsequent toggles
/// update in-memory state; layouts persist the new value back to the cookie via JS.
/// </summary>
public sealed class ThemeState
{
    public const string CookieName = "courtflow-theme";

    public bool IsDarkMode { get; private set; }
    public MudTheme CurrentTheme { get; }
    public event Action? OnChanged;

    public ThemeState(IHttpContextAccessor? accessor = null)
    {
        CurrentTheme = BuildTheme();
        var cookie = accessor?.HttpContext?.Request.Cookies[CookieName];
        IsDarkMode = string.Equals(cookie, "dark", StringComparison.OrdinalIgnoreCase);
    }

    public void SetDarkMode(bool isDark)
    {
        if (IsDarkMode == isDark) return;
        IsDarkMode = isDark;
        OnChanged?.Invoke();
    }

    public void Toggle() => SetDarkMode(!IsDarkMode);

    private static MudTheme BuildTheme() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#245F73",
            PrimaryDarken = "#1E4F60",
            PrimaryLighten = "#3D778C",
            Secondary = "#733E24",
            SecondaryDarken = "#5E321D",
            SecondaryLighten = "#8A4B2B",
            Background = "#F2F0EF",
            Surface = "#FFFFFF",
            AppbarBackground = "#245F73",
            AppbarText = "#F2F0EF",
            TextPrimary = "#1F2A30",
            TextSecondary = "#5D6670",
            Success = "#2E7D32",
            Info = "#245F73",
            Warning = "#F59E0B",
            Error = "#D32F2F",
            ActionDefault = "#5D6670"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#1E4F60",
            PrimaryDarken = "#163D4A",
            PrimaryLighten = "#2F6C82",
            Secondary = "#A56747",
            SecondaryDarken = "#733E24",
            SecondaryLighten = "#BD7C59",
            Background = "#1D2327",
            Surface = "#2A3237",
            AppbarBackground = "#1E4F60",
            AppbarText = "#F2F0EF",
            DrawerBackground = "#232B30",
            DrawerText = "#F2F0EF",
            DrawerIcon = "#BBBDBC",
            TextPrimary = "#F2F0EF",
            TextSecondary = "#D6D8D7",
            LinesDefault = "#3A444A",
            LinesInputs = "#3A444A",
            TableLines = "#3A444A",
            Divider = "#3A444A",
            ActionDefault = "#BBBDBC"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif" },
                FontSize = "0.95rem",
                FontWeight = "400",
                LineHeight = "1.5"
            },
            H6 = new H6Typography
            {
                FontFamily = new[] { "Inter", "Segoe UI", "Roboto", "sans-serif" },
                FontWeight = "600",
                FontSize = "1.125rem",
                LineHeight = "1.5"
            },
            H4 = new H4Typography
            {
                FontFamily = new[] { "Inter", "Segoe UI", "Roboto", "sans-serif" },
                FontWeight = "600"
            },
            Button = new ButtonTypography
            {
                FontFamily = new[] { "Inter", "Segoe UI", "Roboto", "sans-serif" },
                FontWeight = "600",
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px"
        }
    };
}
