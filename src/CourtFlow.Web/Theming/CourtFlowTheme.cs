using MudBlazor;

namespace CourtFlow.Web.Theming;

/// <summary>
/// The one place that holds the brand: both palettes, the typography, the shape, and the logo.
/// No other C# file may contain a hex color or a font name. StylingRulesTests enforces that,
/// and ThemeContrastTests checks every text-on-color pair below for WCAG AA.
/// </summary>
public static class CourtFlowTheme
{
    public const string LogoPath = "CourtFlow.png";
    public const int LogoSize = 44;

    private static readonly string[] FontFamily = ["Inter", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#245F73",
            Secondary = "#733E24",
            Background = "#F2F0EF",
            Surface = "#FFFFFF",
            AppbarBackground = "#245F73",
            AppbarText = "#F2F0EF",
            TextPrimary = "#1F2A30",
            TextSecondary = "#5D6670",
            Success = "#2E7D32",
            Info = "#245F73",
            Warning = "#F59E0B",
            WarningContrastText = "#1F2A30",
            Error = "#D32F2F",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#1E4F60",
            Secondary = "#A56747",
            Background = "#1D2327",
            Surface = "#2A3237",
            AppbarBackground = "#1E4F60",
            AppbarText = "#F2F0EF",
            DrawerBackground = "#232B30",
            DrawerText = "#F2F0EF",
            DrawerIcon = "#D6D8D7",
            TextPrimary = "#F2F0EF",
            TextSecondary = "#D6D8D7",
            LinesDefault = "#3A444A",
            LinesInputs = "#3A444A",
            TableLines = "#3A444A",
            Divider = "#3A444A",
            Success = "#2E7D32",
            Info = "#245F73",
            Warning = "#F59E0B",
            WarningContrastText = "#1F2A30",
            Error = "#D32F2F",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = FontFamily,
                FontSize = "0.95rem",
                FontWeight = "400",
                LineHeight = "1.5",
            },
            H4 = new H4Typography
            {
                FontFamily = FontFamily,
                FontWeight = "600",
            },
            H6 = new H6Typography
            {
                FontFamily = FontFamily,
                FontWeight = "600",
            },
            Button = new ButtonTypography
            {
                FontFamily = FontFamily,
                FontWeight = "600",
                TextTransform = "none",
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
        },
    };
}
