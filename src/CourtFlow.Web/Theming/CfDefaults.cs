using MudBlazor;

namespace CourtFlow.Web.Theming;

/// <summary>Typed component defaults the Cf wrappers and the layout read, so pages never repeat them.</summary>
public static class CfDefaults
{
    public const Variant FieldVariant = Variant.Outlined;
    public const Margin FieldMargin = Margin.Dense;
    public const Size ButtonSize = Size.Small;

    /// <summary>
    /// Filled, so alert text is the status color's contrast text. MudBlazor's text and outlined
    /// variants use the status color itself as text, which fails contrast on the dark surface.
    /// </summary>
    public const Variant AlertVariant = Variant.Filled;
    public const int AppBarElevation = 1;
    public const MaxWidth ContentMaxWidth = MaxWidth.ExtraExtraLarge;
    public const MaxWidth PublicContentMaxWidth = MaxWidth.Large;
    public const MaxWidth DialogMaxWidth = MaxWidth.Small;
    public const MaxWidth WideDialogMaxWidth = MaxWidth.Large;

    /// <summary>
    /// MudBlazor's own <c>DragAndDrop</c> mode draws its own drop area. This keeps the CfField look by
    /// stretching the native file input, invisible, over the drop target so a dropped file lands on it.
    /// These are MudBlazor's own utility classes and an opacity class, not a visual choice.
    /// </summary>
    public const string DropZoneClass = "relative";
    public const string DropZoneInputClass = "absolute mud-width-full mud-height-full overflow-hidden z-10 cf-drop-zone-input";

    /// <summary>MudBlazor's own class for the palette's secondary text color.</summary>
    public const string SecondaryTextClass = "mud-text-secondary";

    /// <summary>MudBlazor's flex utility that centers a row's children on the cross axis.</summary>
    public const string AlignCenterClass = "align-center";

    /// <summary>MudBlazor's utility that centers the text inside an element, including a rank field.</summary>
    public const string CenterTextClass = "mud-typography-align-center";

    /// <summary>
    /// The brand link and its name in an app bar. MudBlazor 8.13 ships no truncation utility, so the
    /// two rules behind these names live in <c>wwwroot/css/app.css</c>. They let the word CourtFlow
    /// give way first on a narrow bar, which keeps the theme toggle and Login fully visible.
    /// </summary>
    public const string BrandLinkClass = "cf-brand";
    public const string BrandNameClass = "cf-brand-name";
}