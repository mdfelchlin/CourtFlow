using System.Diagnostics;
using System.Xml.Linq;

namespace CourtFlow.Web.Tests;

/// <summary>
/// Reads the product tree and fails when a NuGet version drifts out of central management.
/// </summary>
public class PackageRulesTests
{
    private static readonly string SolutionDirectory = FindSolutionDirectory();
    private static readonly string RepoRoot = Directory.GetParent(SolutionDirectory)!.FullName;

    private static readonly string[] PackageIds =
    [
        "Aspire.Hosting.AppHost",
        "Aspire.Hosting.PostgreSQL",
        "Aspire.Hosting.Testing",
        "bunit",
        "CommunityToolkit.Aspire.Hosting.MailPit",
        "coverlet.MTP",
        "Microsoft.AspNetCore.Components.Authorization",
        "Microsoft.AspNetCore.Components.WebAssembly",
        "Microsoft.AspNetCore.Components.WebAssembly.DevServer",
        "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore",
        "Microsoft.AspNetCore.Mvc.Testing",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Design",
        "Microsoft.EntityFrameworkCore.Relational",
        "Microsoft.NET.Test.Sdk",
        "MudBlazor",
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "Testcontainers.PostgreSql",
        "xunit.runner.visualstudio",
        "xunit.v3",
    ];

    [Fact]
    public void Central_file_is_the_only_one_and_enables_central_management()
    {
        var found = Directory.EnumerateFiles(RepoRoot, "Directory.Packages.props", SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path))
            .ToList();

        var central = Path.Combine(SolutionDirectory, "Directory.Packages.props");
        Assert.Equal([central], found.Select(Path.GetFullPath));

        var document = XDocument.Load(central);
        var enabled = document.Descendants()
            .Single(element => element.Name.LocalName == "ManagePackageVersionsCentrally");
        Assert.Equal("true", enabled.Value.Trim(), ignoreCase: true);
    }

    [Fact]
    public void Each_package_id_is_declared_once_as_an_exact_stable_version()
    {
        var versions = PackageVersions();

        Assert.Equal(Ordered(PackageIds), versions.Select(version => version.Id));
        Assert.All(versions, version => Assert.Matches(@"^\d+(\.\d+)+$", version.Version));
    }

    [Fact]
    public void Package_references_set_no_version_and_use_only_the_declared_ids()
    {
        var references = ProjectFiles()
            .SelectMany(project => ProjectReferences(project.path))
            .ToList();

        Assert.Empty(references.Where(reference => reference.HasVersion).Select(reference => reference.Id));
        Assert.Equal(Ordered(PackageIds), references.Select(reference => reference.Id).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Domain_and_application_reference_no_packages()
    {
        foreach (var name in new[] { "CourtFlow.Domain", "CourtFlow.Application" })
        {
            var project = Path.Combine(SolutionDirectory, name, $"{name}.csproj");
            Assert.Empty(ProjectReferences(project));
        }
    }

    [Fact]
    public void Design_and_dev_server_keep_their_asset_metadata()
    {
        var design = ProjectReferences(Path.Combine(SolutionDirectory, "CourtFlow.Infrastructure", "CourtFlow.Infrastructure.csproj"))
            .Single(reference => reference.Id == "Microsoft.EntityFrameworkCore.Design");
        Assert.Equal("runtime; build; native; contentfiles; analyzers; buildtransitive", design.IncludeAssets);
        Assert.Equal("all", design.PrivateAssets);

        var devServer = ProjectReferences(Path.Combine(SolutionDirectory, "CourtFlow.Web", "CourtFlow.Web.csproj"))
            .Single(reference => reference.Id == "Microsoft.AspNetCore.Components.WebAssembly.DevServer");
        Assert.Equal("all", devServer.PrivateAssets);
    }

    [Fact]
    public void Every_project_targets_net10()
    {
        var offenders = ProjectFiles()
            .Where(project => project.document.Descendants().Where(element => element.Name.LocalName == "TargetFramework").All(element => element.Value.Trim() != "net10.0"))
            .Select(project => project.path)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void App_host_sdk_version_matches_the_app_host_package()
    {
        var project = XDocument.Load(Path.Combine(SolutionDirectory, "CourtFlow.AppHost", "CourtFlow.AppHost.csproj"));
        var sdk = project.Descendants().Single(element => element.Name.LocalName == "Sdk" && element.Attribute("Name")?.Value == "Aspire.AppHost.Sdk");
        var package = PackageVersions().Single(version => version.Id == "Aspire.Hosting.AppHost");

        Assert.Equal(package.Version, sdk.Attribute("Version")!.Value);
    }

    [Fact]
    public void Vulnerability_warnings_are_not_suppressed()
    {
        var codes = new[] { "NU1901", "NU1902", "NU1903", "NU1904" };
        var files = ProjectFiles().Select(project => project.path)
            .Append(Path.Combine(SolutionDirectory, "Directory.Packages.props"));
        var offenders = files
            .Where(path => codes.Any(code => File.ReadAllText(path).Contains(code, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task A_project_version_fails_the_build()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "courtflow-cpm-" + Guid.NewGuid().ToString("N")));
        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "Directory.Packages.props"),
                """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                  </ItemGroup>
                </Project>
                """);
            var project = Path.Combine(directory.FullName, "Temp.csproj");
            File.WriteAllText(
                project,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                  </ItemGroup>
                </Project>
                """);

            var start = new ProcessStartInfo("dotnet", $"build \"{project}\" --nologo -nodeReuse:false")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(start)!;
            var cancellation = TestContext.Current.CancellationToken;
            var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
            var stderr = process.StandardError.ReadToEndAsync(cancellation);
            try
            {
                await process.WaitForExitAsync(cancellation).WaitAsync(TimeSpan.FromSeconds(120), cancellation);
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("dotnet build timed out");
            }

            var output = await stdout + await stderr;

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("NU1008", output);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Rule_is_always_on_and_states_the_add_limits()
    {
        var rule = File.ReadAllText(Path.Combine(RepoRoot, ".cursor", "rules", "current-packages.mdc"));

        Assert.Contains("alwaysApply: true", rule);
        Assert.Contains("specifically instructed", rule);
        Assert.Contains("src/Directory.Packages.props", rule);
        Assert.Contains("prerelease", rule);
        Assert.Contains("known vulnerability", rule);
        Assert.Contains("deprecation notice", rule);
        Assert.Contains("update-packages", rule);
    }

    [Fact]
    public void Skill_asks_before_each_breaking_change_and_includes_transitive_vulnerabilities()
    {
        var skill = File.ReadAllText(Path.Combine(RepoRoot, ".cursor", "skills", "update-packages", "SKILL.md"));

        Assert.Contains("one package at a time", skill);
        Assert.Contains("--include-transitive", skill);
        Assert.Contains("net10.0", skill);
        Assert.Contains("does not add a package id", skill);
    }

    private static IReadOnlyList<(string Id, string Version)> PackageVersions()
    {
        var document = XDocument.Load(Path.Combine(SolutionDirectory, "Directory.Packages.props"));
        return document.Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .Select(element => (element.Attribute("Include")!.Value, element.Attribute("Version")!.Value))
            .OrderBy(version => version.Item1, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<(string path, XDocument document)> ProjectFiles() =>
        Directory.EnumerateFiles(SolutionDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path))
            .Select(path => (path, XDocument.Load(path)));

    private static IReadOnlyList<PackageReference> ProjectReferences(string path)
    {
        var document = XDocument.Load(path);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => new PackageReference(
                element.Attribute("Include")!.Value,
                element.Attribute("Version") is not null || element.Elements().Any(child => child.Name.LocalName == "Version"),
                element.Attribute("IncludeAssets")?.Value ?? ChildValue(element, "IncludeAssets"),
                element.Attribute("PrivateAssets")?.Value ?? ChildValue(element, "PrivateAssets")))
            .ToList();
    }

    private static string? ChildValue(XElement element, string name) =>
        element.Elements().SingleOrDefault(child => child.Name.LocalName == name)?.Value;

    private static bool IsIgnored(string path)
    {
        var relative = Path.GetRelativePath(RepoRoot, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" or "node_modules");
    }

    private static IEnumerable<string> Ordered(IEnumerable<string> ids) =>
        ids.Order(StringComparer.Ordinal);

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CourtFlow.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("CourtFlow.slnx was not found above the test assembly.");

        return directory.FullName;
    }

    private sealed record PackageReference(string Id, bool HasVersion, string? IncludeAssets, string? PrivateAssets);
}
