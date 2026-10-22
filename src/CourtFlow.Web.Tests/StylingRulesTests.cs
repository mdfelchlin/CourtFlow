using System.Text.RegularExpressions;

using CourtFlow.Web.Theming;

namespace CourtFlow.Web.Tests;

/// <summary>
/// Reads the CourtFlow.Web source tree and fails when styling leaks out of the typed theme:
/// scoped CSS, Bootstrap, inline styles, hex colors in markup, or hand-written class names.
/// </summary>
public class StylingRulesTests
{
    private static readonly Regex HexColor = new(@"#[0-9a-fA-F]{3,8}\b", RegexOptions.Compiled);
    private static readonly Regex StyleAttribute = new(@"\bstyle\s*=", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LiteralClass = new(@"\b[Cc]lass=""([^""@]*)""", RegexOptions.Compiled);
    private static readonly Regex FontName = new(@"\bInter\b", RegexOptions.Compiled);
    private static readonly Regex SpacingUtility = new(@"^[pm][atbrlsxye]-(n?\d+|auto)$", RegexOptions.Compiled);

    private static readonly string WebRoot = FindWebRoot();

    [Fact]
    public void NoScopedCssFileExists()
    {
        var scoped = SourceFiles("*.razor.css").ToList();

        Assert.Empty(scoped);
    }

    [Fact]
    public void BootstrapIsGone()
    {
        Assert.False(Directory.Exists(Path.Combine(WebRoot, "wwwroot", "lib", "bootstrap")));
    }

    [Fact]
    public void RazorFilesHaveNoStyleAttributeOrHexColor()
    {
        var offenders = SourceFiles("*.razor")
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(file => StyleAttribute.IsMatch(file.text) || HexColor.IsMatch(file.text))
            .Select(file => Path.GetRelativePath(WebRoot, file.path))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void LiteralClassValuesAreOnlySpacingUtilities()
    {
        var offenders = new List<string>();
        foreach (var path in SourceFiles("*.razor"))
        {
            foreach (Match match in LiteralClass.Matches(File.ReadAllText(path)))
            {
                var tokens = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Any(token => !SpacingUtility.IsMatch(token)))
                    offenders.Add($"{Path.GetRelativePath(WebRoot, path)}: {match.Value}");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void OnlyTheThemeFileHoldsHexColorsOrTheFontName()
    {
        var offenders = SourceFiles("*.cs")
            .Where(path => !path.EndsWith(Path.Combine("Theming", "CourtFlowTheme.cs"), StringComparison.Ordinal))
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(file => HexColor.IsMatch(file.text) || FontName.IsMatch(file.text))
            .Select(file => Path.GetRelativePath(WebRoot, file.path))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AppCssUsesOnlyTheLightBackgroundPrimaryAndTextColors()
    {
        var palette = CourtFlowTheme.Theme.PaletteLight;
        var allowed = new[] { palette.Background, palette.Primary, palette.TextPrimary }
            .Select(color => Hex(color.R, color.G, color.B))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var css = File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "css", "app.css"));
        var found = HexColor.Matches(css).Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.NotEmpty(found);
        Assert.All(found, color => Assert.Contains(color, allowed));
    }

    private static string Hex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(WebRoot, pattern, SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(WebRoot, path);
                var top = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                return top is not ("bin" or "obj");
            });

    private static string FindWebRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CourtFlow.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("CourtFlow.slnx was not found above the test assembly.");

        return Path.Combine(directory.FullName, "CourtFlow.Web");
    }
}