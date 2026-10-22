using System.Xml.Linq;

namespace CourtFlow.Web.Tests;

/// <summary>
/// Reads the product tree and fails when shared build checks drift from the engineering-practices rule.
/// </summary>
public class EngineeringPracticesTests
{
    private static readonly string SolutionDirectory = FindSolutionDirectory();
    private static readonly string RepoRoot = Directory.GetParent(SolutionDirectory)!.FullName;

    private static readonly string[] AnalysisProperties =
    [
        "AnalysisLevel",
        "AnalysisMode",
        "TreatWarningsAsErrors",
        "EnforceCodeStyleInBuild",
    ];

    private static readonly string[] NullableProperties = ["Nullable", "ImplicitUsings"];

    [Fact]
    public void DirectoryBuildPropsHoldsTheSharedAnalysisSettings()
    {
        var props = Path.Combine(SolutionDirectory, "Directory.Build.props");
        var offenders = RepoFiles()
            .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(props), StringComparison.OrdinalIgnoreCase))
            .Where(path => AnalysisProperties.Any(name => SetsProperty(File.ReadAllText(path), name)))
            .ToList();

        Assert.Empty(offenders);

        var document = XDocument.Load(props);
        Assert.Equal("latest", Value(document, "AnalysisLevel"));
        Assert.Equal("Recommended", Value(document, "AnalysisMode"));
        Assert.Equal("true", Value(document, "TreatWarningsAsErrors"));
        Assert.Equal("true", Value(document, "EnforceCodeStyleInBuild"));
        Assert.Equal("enable", Value(document, "Nullable"));
        Assert.Equal("enable", Value(document, "ImplicitUsings"));
    }

    [Fact]
    public void ProjectFilesDoNotWeakenTheSharedSettings()
    {
        foreach (var (path, document) in ProjectFiles())
        {
            var analysis = document.Descendants()
                .Where(element => AnalysisProperties.Contains(element.Name.LocalName, StringComparer.Ordinal))
                .Select(element => element.Name.LocalName)
                .ToList();
            Assert.Empty(analysis);

            foreach (var name in NullableProperties)
            {
                var disabled = document.Descendants()
                    .Where(element => element.Name.LocalName == name && element.Value.Trim().Equals("disable", StringComparison.OrdinalIgnoreCase))
                    .Select(element => path)
                    .ToList();
                Assert.Empty(disabled);
            }
        }
    }

    [Fact]
    public void OnlyInfrastructureMaySuppressMigrationCompilerWarnings()
    {
        foreach (var (path, document) in ProjectFiles())
        {
            var noWarn = document.Descendants().Where(element => element.Name.LocalName == "NoWarn").ToList();
            if (noWarn.Count == 0)
                continue;

            Assert.Equal("CourtFlow.Infrastructure.csproj", Path.GetFileName(path));
            Assert.Contains(
                document.DescendantNodes().OfType<XComment>(),
                comment => comment.Value.Contains("Migrations", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void RootEditorconfigMarksMigrationsAsGenerated()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, ".editorconfig"));
        Assert.Contains("root = true", text, StringComparison.Ordinal);

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var section = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "[src/CourtFlow.Infrastructure/Migrations/**]")
                section = i;
        }

        Assert.True(section >= 0);
        var generated = false;
        for (var i = section + 1; i < lines.Length && !lines[i].TrimStart().StartsWith('['); i++)
        {
            if (lines[i].Trim() == "generated_code = true")
                generated = true;
        }

        Assert.True(generated);
    }

    [Fact]
    public void HandWrittenPragmasNameAReason()
    {
        var pragma = "#pragma warning " + "disable";
        var offenders = new List<string>();
        foreach (var path in SourceFiles())
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(pragma, StringComparison.Ordinal)
                    && !lines[i].Contains("//", StringComparison.Ordinal))
                {
                    offenders.Add(path + ":" + (i + 1));
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void EngineeringPracticesRuleStaysOn()
    {
        var rule = File.ReadAllText(Path.Combine(RepoRoot, ".cursor", "rules", "engineering-practices.mdc"));

        Assert.Contains("alwaysApply: true", rule, StringComparison.Ordinal);
        Assert.Contains("*.razor.css", rule, StringComparison.Ordinal);
        Assert.Contains("service-defaults", rule, StringComparison.Ordinal);
        Assert.Contains("courtflow-session", rule, StringComparison.Ordinal);
        Assert.Contains("MM/dd/yyyy", rule, StringComparison.Ordinal);
        Assert.Contains("Recommended", rule, StringComparison.Ordinal);
        Assert.Contains("verify-engineering-practices", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyEngineeringPracticesSkillChecksTheBranch()
    {
        var skill = File.ReadAllText(Path.Combine(RepoRoot, ".cursor", "skills", "verify-engineering-practices", "SKILL.md"));

        Assert.Contains("default branch", skill, StringComparison.Ordinal);
        Assert.Contains("uncommitted", skill, StringComparison.Ordinal);
        Assert.Contains("dotnet format", skill, StringComparison.Ordinal);
        Assert.Contains("src/CourtFlow.slnx", skill, StringComparison.Ordinal);
        Assert.Contains("does not move a type", skill, StringComparison.Ordinal);
        Assert.Contains("Migrations", skill, StringComparison.Ordinal);
        Assert.Contains("tools/logo/", skill, StringComparison.Ordinal);
        Assert.Contains("update-packages", skill, StringComparison.Ordinal);
        Assert.Contains("each changed file", skill, StringComparison.Ordinal);
        Assert.Contains("Stop without editing when the fix would change accepted behavior", skill, StringComparison.Ordinal);
    }

    private static string Value(XDocument document, string name) =>
        document.Descendants().Single(element => element.Name.LocalName == name).Value.Trim();

    private static bool SetsProperty(string text, string name) =>
        text.Contains("<" + name + ">", StringComparison.Ordinal)
        || text.Contains("<" + name + " ", StringComparison.Ordinal);

    private static List<(string path, XDocument document)> ProjectFiles() =>
        Directory.EnumerateFiles(SolutionDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path))
            .Select(path => (path, XDocument.Load(path)))
            .ToList();

    private static IEnumerable<string> RepoFiles() =>
        Directory.EnumerateFiles(RepoRoot, "*", SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path));

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(SolutionDirectory, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsIgnored(path) && !IsMigrations(path));

    private static bool IsMigrations(string path)
    {
        var relative = Path.GetRelativePath(SolutionDirectory, path);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 2 && parts[0] == "CourtFlow.Infrastructure" && parts[1] == "Migrations";
    }

    private static bool IsIgnored(string path)
    {
        var relative = Path.GetRelativePath(RepoRoot, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" or "node_modules");
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CourtFlow.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("CourtFlow.slnx was not found above the test assembly.");

        return directory.FullName;
    }
}
