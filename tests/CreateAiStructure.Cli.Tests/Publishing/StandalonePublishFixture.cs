using System.Diagnostics;
using CreateAiStructure.Tests.Shared;

namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>
/// Publishes <c>create-ai-structure.exe</c> once per test class as a single-file, self-contained <c>win-x64</c> app into
/// a temp directory, then copies <b>only</b> the executable into a separate isolated temp directory outside the
/// repository. Does nothing on non-Windows hosts, where the tests are skipped. All temp directories are deleted on dispose.
/// </summary>
public sealed class StandalonePublishFixture : IAsyncLifetime
{
    /// <summary>The published executable file name.</summary>
    public const string ExecutableName = "create-ai-structure.exe";

    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(10);

    /// <summary>The <c>dotnet</c> host running the tests (set by the .NET CLI), else <c>dotnet</c> from <c>PATH</c>.</summary>
    private static string DotnetHost => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

    private readonly List<TemporaryDirectory> _directories = [];

    /// <summary>Gets the exit code of <c>dotnet publish</c> (<c>-1</c> when it did not run).</summary>
    public int PublishExitCode { get; private set; } = -1;

    /// <summary>Gets the stdout and stderr of <c>dotnet publish</c>.</summary>
    public string PublishOutput { get; private set; } = string.Empty;

    /// <summary>Gets how long the publish took.</summary>
    public TimeSpan PublishDuration { get; private set; }

    /// <summary>Gets the isolated directory that holds nothing but the copied executable.</summary>
    public string IsolatedDirectory { get; private set; } = string.Empty;

    /// <summary>Gets the full path of the copied executable inside <see cref="IsolatedDirectory"/>.</summary>
    public string ExecutablePath => Path.Combine(IsolatedDirectory, ExecutableName);

    /// <summary>Creates a fresh, empty temp directory that is deleted when the fixture is disposed.</summary>
    /// <param name="purpose">A short label used in the directory name.</param>
    /// <returns>The absolute directory path.</returns>
    public string CreateTemporaryDirectory(string purpose) => Track(new TemporaryDirectory(purpose)).FullPath;

    public async Task InitializeAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var publishDirectory = CreateTemporaryDirectory("publish");
        var project = Path.Combine(ProjectFile.RepositoryRoot, "src", "CreateAiStructure.Cli", "CreateAiStructure.Cli.csproj");

        var stopwatch = Stopwatch.StartNew();
        var result = await ExternalProcess.RunAsync(
            DotnetHost,
            [
                "publish", project,
                "-c", "Release",
                "-r", "win-x64",
                "--self-contained", "true",
                "-o", publishDirectory,
                // No persistent MSBuild nodes or compiler server: they would inherit and hold the redirected pipes open.
                "-nodeReuse:false",
                "-p:UseSharedCompilation=false",
                "-tl:off",
                "-clp:NoSummary",
            ],
            ProjectFile.RepositoryRoot,
            PublishTimeout,
            new Dictionary<string, string?>
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
                ["MSBUILDDISABLENODEREUSE"] = "1",
            });
        PublishDuration = stopwatch.Elapsed;
        PublishExitCode = result.ExitCode;
        PublishOutput = result.StandardOutput + result.StandardError;

        var publishedExecutable = Path.Combine(publishDirectory, ExecutableName);
        if (PublishExitCode != 0 || !File.Exists(publishedExecutable))
        {
            return;
        }

        IsolatedDirectory = CreateTemporaryDirectory("isolated");
        File.Copy(publishedExecutable, ExecutablePath);
    }

    public Task DisposeAsync()
    {
        foreach (var directory in _directories)
        {
            directory.Dispose();
        }

        return Task.CompletedTask;
    }

    private TemporaryDirectory Track(TemporaryDirectory directory)
    {
        _directories.Add(directory);
        return directory;
    }
}
