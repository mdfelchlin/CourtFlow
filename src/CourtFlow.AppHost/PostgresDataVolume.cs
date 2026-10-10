using System.Diagnostics;
using System.Text.Json;

namespace CourtFlow.AppHost;

public enum PostgresVolumeAction
{
    UseExisting,
    CopyFromSource,
    StartEmpty,
}

public readonly record struct PostgresVolumeDecision(PostgresVolumeAction Action, string? SourceVolume);

public static class PostgresDataVolume
{
    public const string Name = "courtflow-postgres";
    public const string SkipEnvironmentVariable = "COURTFLOW_SKIP_VOLUME_ADOPTION";

    const string DataDestination = "/var/lib/postgresql/data";
    const string ComposeServiceLabel = "com.docker.compose.service";
    const string ComposeWorkingDirectoryLabel = "com.docker.compose.project.working_dir";
    const string ComposeContainerName = "courtflow-postgres-1";

    public static PostgresVolumeDecision Decide(bool namedVolumeExists, string? composeSourceVolume)
    {
        if (namedVolumeExists)
        {
            return new PostgresVolumeDecision(PostgresVolumeAction.UseExisting, null);
        }

        if (!string.IsNullOrWhiteSpace(composeSourceVolume))
        {
            return new PostgresVolumeDecision(PostgresVolumeAction.CopyFromSource, composeSourceVolume);
        }

        return new PostgresVolumeDecision(PostgresVolumeAction.StartEmpty, null);
    }

    public static void Adopt()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(SkipEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        var docker = new DockerCli();
        var namedVolumeExists = docker.VolumeExists(Name);
        ComposeSource? source = namedVolumeExists ? null : docker.FindComposeSource(RepoRoot());
        var decision = Decide(namedVolumeExists, source?.VolumeName);

        if (decision.Action != PostgresVolumeAction.CopyFromSource)
        {
            return;
        }

        Console.WriteLine($"Copying existing Postgres data from {decision.SourceVolume} into {Name}.");
        docker.StopContainer(source!.ContainerId);
        docker.CreateVolume(Name);
        try
        {
            docker.CopyVolume(decision.SourceVolume!, Name);
        }
        catch
        {
            docker.TryRemoveVolume(Name);
            throw;
        }
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "CourtFlow.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the CourtFlow repository root.");
    }

    sealed record ComposeSource(string ContainerId, string VolumeName);

    sealed class DockerCli
    {
        public bool VolumeExists(string name)
        {
            var result = Run("volume", "inspect", name);
            if (result.ExitCode == 0)
            {
                return true;
            }

            if (result.StdErr.Contains("No such volume", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw DockerFailed(result);
        }

        public ComposeSource? FindComposeSource(string repoRoot)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddIds(ids, Run("ps", "-aq", "--filter", $"label={ComposeServiceLabel}=postgres"));
            AddIds(ids, Run("ps", "-aq", "--filter", $"name=^{ComposeContainerName}$"));

            ComposeSource? nameMatch = null;
            foreach (var id in ids)
            {
                var inspect = Run("inspect", id);
                if (inspect.ExitCode != 0)
                {
                    throw DockerFailed(inspect);
                }

                var source = ReadSource(inspect.StdOut, repoRoot);
                if (source is null)
                {
                    continue;
                }

                if (source.WorkingDirectoryMatches)
                {
                    return new ComposeSource(source.ContainerId, source.VolumeName);
                }

                nameMatch ??= new ComposeSource(source.ContainerId, source.VolumeName);
            }

            return nameMatch;
        }

        public void StopContainer(string containerId) =>
            RequireOk(Run("stop", containerId));

        public void CreateVolume(string name) =>
            RequireOk(Run("volume", "create", name));

        public void CopyVolume(string source, string destination) =>
            RequireOk(Run(
                "run", "--rm",
                "-v", $"{source}:/from:ro",
                "-v", $"{destination}:/to",
                "postgres:16",
                "sh", "-c", "cp -a /from/. /to/"));

        public void TryRemoveVolume(string name) =>
            Run("volume", "rm", name);

        static void AddIds(HashSet<string> ids, DockerResult result)
        {
            if (result.ExitCode != 0)
            {
                throw DockerFailed(result);
            }

            foreach (var line in result.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                ids.Add(line.Trim());
            }
        }

        static InspectedSource? ReadSource(string json, string repoRoot)
        {
            using var document = JsonDocument.Parse(json);
            var container = document.RootElement[0];
            var name = container.GetProperty("Name").GetString()?.TrimStart('/') ?? "";
            var labels = container.GetProperty("Config").GetProperty("Labels");
            var service = Label(labels, ComposeServiceLabel);
            var workingDirectory = Label(labels, ComposeWorkingDirectoryLabel);
            var namedContainer = string.Equals(name, ComposeContainerName, StringComparison.OrdinalIgnoreCase);
            var workingDirectoryMatches = service == "postgres" &&
                workingDirectory is not null &&
                SameDirectory(workingDirectory, repoRoot);

            if (!workingDirectoryMatches && !namedContainer)
            {
                return null;
            }

            if (!container.TryGetProperty("Mounts", out var mounts))
            {
                return null;
            }

            foreach (var mount in mounts.EnumerateArray())
            {
                var destination = mount.GetProperty("Destination").GetString();
                var volumeName = mount.TryGetProperty("Name", out var volume) ? volume.GetString() : null;
                if (destination == DataDestination && !string.IsNullOrWhiteSpace(volumeName))
                {
                    return new InspectedSource(container.GetProperty("Id").GetString()!, volumeName, workingDirectoryMatches);
                }
            }

            return null;
        }

        static string? Label(JsonElement labels, string name) =>
            labels.ValueKind == JsonValueKind.Object && labels.TryGetProperty(name, out var value)
                ? value.GetString()
                : null;

        static bool SameDirectory(string left, string right)
        {
            static string Normalize(string value) =>
                value.Replace('/', '\\').TrimEnd('\\');

            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        static DockerResult Run(params string[] args)
        {
            using var process = new Process();
            process.StartInfo.FileName = "docker";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            foreach (var arg in args)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            try
            {
                process.Start();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                throw new InvalidOperationException("Docker is required to start Postgres.", ex);
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new DockerResult(process.ExitCode, stdout, stderr);
        }

        static void RequireOk(DockerResult result)
        {
            if (result.ExitCode != 0)
            {
                throw DockerFailed(result);
            }
        }

        static InvalidOperationException DockerFailed(DockerResult result) =>
            new($"docker failed ({result.ExitCode}): {result.StdErr.Trim()}");

        sealed record InspectedSource(string ContainerId, string VolumeName, bool WorkingDirectoryMatches);

        readonly record struct DockerResult(int ExitCode, string StdOut, string StdErr);
    }
}
