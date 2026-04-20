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

    public ThemeState(IHttpContextAccessor accessor)
    {
        CurrentTheme = BuildTheme();
        var cookie = accessor.HttpContext?.Request.Cookies[CookieName];
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
            Primary = "#3949AB",
            PrimaryDarken = "#26328A",
            PrimaryLighten = "#5C6BC0",
            Secondary = "#A8E10C",
            SecondaryDarken = "#7FAE00",
            SecondaryLighten = "#C5F04E",
            Background = "#F7F8FC",
            Surface = "#FFFFFF",
            AppbarBackground = "#3949AB",
            AppbarText = "#FFFFFF",
            TextPrimary = "#1A1F36",
            TextSecondary = "#4A5072",
            Success = "#2E7D32",
            Info = "#3949AB",
            Warning = "#F59E0B",
            Error = "#D32F2F",
            ActionDefault = "#4A5072"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7986CB",
            PrimaryDarken = "#3949AB",
            PrimaryLighten = "#9FA8DA",
            Secondary = "#C5F04E",
            SecondaryDarken = "#A8E10C",
            SecondaryLighten = "#D9F77C",
            Background = "#0F1320",
            Surface = "#1A1F36",
            AppbarBackground = "#1A1F36",
            AppbarText = "#E6E8F2",
            DrawerBackground = "#151A2B",
            DrawerText = "#E6E8F2",
            DrawerIcon = "#A7ADC7",
            TextPrimary = "#E6E8F2",
            TextSecondary = "#A7ADC7",
            LinesDefault = "#2A314D",
            LinesInputs = "#2A314D",
            TableLines = "#2A314D",
            Divider = "#2A314D",
            ActionDefault = "#A7ADC7"
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
