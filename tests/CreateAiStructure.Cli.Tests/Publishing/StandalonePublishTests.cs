using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Infrastructure.T3;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;
using Xunit.Abstractions;

namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>
/// AC-8/AC-9/AC-10: the single-file, self-contained <c>win-x64</c> publish produces a lone
/// <c>create-ai-structure.exe</c> that runs from an isolated directory with no companion DLLs and no
/// <c>templates/</c> folder, including a full offline scaffold run against <c>.cmd</c> tool shims.
/// </summary>
[Trait("Category", "Publish")]
public sealed class StandalonePublishTests : IClassFixture<StandalonePublishFixture>
{
    private const string RepositoryName = "standalone-demo";

    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    private static readonly string[] SupportedTypes = ["webApp", "api", "console", "MAUI"];

    private readonly StandalonePublishFixture _fixture;
    private readonly ITestOutputHelper _output;

    public StandalonePublishTests(StandalonePublishFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [WindowsOnlyFact]
    public void Publish_succeeds_without_warnings()
    {
        _output.WriteLine($"dotnet publish took {_fixture.PublishDuration.TotalSeconds:F1} s.");

        _fixture.PublishExitCode.Should().Be(0, "publish output:{0}{1}", Environment.NewLine, _fixture.PublishOutput);
        _fixture.PublishOutput.Should().NotContainEquivalentOf("warning");
    }

    [WindowsOnlyFact]
    public void Isolated_directory_outside_the_repository_holds_only_the_executable()
    {
        RequirePublishedExecutable();

        var isolated = Path.GetFullPath(_fixture.IsolatedDirectory);
        var repositoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectFile.RepositoryRoot)) + Path.DirectorySeparatorChar;
        isolated.Should().NotStartWithEquivalentOf(repositoryRoot);

        Directory.EnumerateFileSystemEntries(isolated, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Should().Equal(StandalonePublishFixture.ExecutableName);
        Directory.EnumerateFiles(isolated, "*.dll", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.Exists(Path.Combine(isolated, "templates")).Should().BeFalse();
    }

    [WindowsOnlyFact]
    public async Task Help_lists_every_supported_project_type()
    {
        var result = await RunExecutableAsync(["--help"]);

        result.ExitCode.Should().Be(CliExitCodes.Success, "{0}", result.Describe());
        result.StandardOutput.Should().ContainAll(SupportedTypes);
        result.StandardError.Should().BeEmpty();
    }

    [WindowsOnlyFact]
    public async Task Version_prints_the_tool_version()
    {
        var result = await RunExecutableAsync(["--version"]);

        result.ExitCode.Should().Be(CliExitCodes.Success, "{0}", result.Describe());
        result.StandardOutput.Trim().Should().Be("create-ai-structure 1.0.0");
    }

    [WindowsOnlyFact]
    public async Task Unsupported_type_is_a_usage_error_listing_every_supported_type()
    {
        var result = await RunExecutableAsync(["--type", "invalid", "--repo", "demo"]);

        result.ExitCode.Should().Be(CliExitCodes.UsageError, "{0}", result.Describe());
        result.StandardError.Should().Contain("ProjectType.Unsupported").And.ContainAll(SupportedTypes);
    }

    [WindowsOnlyFact]
    public async Task Scaffold_run_against_offline_tool_shims_materializes_every_template()
    {
        using var sandbox = new StubToolSandbox();

        var result = await RunExecutableAsync(["--type", "webApp", "--repo", RepositoryName], sandbox);

        result.ExitCode.Should().Be(CliExitCodes.Success, "{0}", result.Describe());
        result.StandardOutput.Should().Contain(T3AppLauncher.ImportReminderMessage, "{0}", result.Describe());
        result.StandardOutput.Should()
            .Contain(CliOutputFormatter.T3AlreadyRunningMessage, "the t3 shim must keep the launcher off its cold-start path")
            .And.NotContain(CliOutputFormatter.T3StartedMessage, "no real T3 Code desktop app may be started");

        var repositoryDirectory = Path.Combine(sandbox.WorkingDirectory, RepositoryName);
        var files = Directory.EnumerateFiles(repositoryDirectory, "*", SearchOption.AllDirectories).ToList();
        files.Should().HaveCount(24)
            .And.HaveCount(TemplateManifest.For(ProjectType.Parse("webApp").Value).Files.Count);
        foreach (var file in files)
        {
            (await File.ReadAllTextAsync(file)).Should().NotContain("{{", "'{0}' must have every token resolved", file);
        }
    }

    private async Task<ExternalProcessResult> RunExecutableAsync(IReadOnlyList<string> arguments)
    {
        using var sandbox = new StubToolSandbox();
        return await RunExecutableAsync(arguments, sandbox);
    }

    /// <summary>
    /// Every invocation runs inside a <see cref="StubToolSandbox"/>: shim-only <c>PATH</c>, no GitHub credentials, sandboxed
    /// <c>gh</c> config and install directories, and a non-existent T3 desktop override, never the developer's real environment.
    /// </summary>
    private async Task<ExternalProcessResult> RunExecutableAsync(IReadOnlyList<string> arguments, StubToolSandbox sandbox)
    {
        RequirePublishedExecutable();

        return await ExternalProcess.RunAsync(
            _fixture.ExecutablePath,
            arguments,
            sandbox.WorkingDirectory,
            RunTimeout,
            sandbox.Environment);
    }

    private void RequirePublishedExecutable() =>
        File.Exists(_fixture.ExecutablePath).Should().BeTrue(
            "the publish must have produced the executable; publish output:{0}{1}", Environment.NewLine, _fixture.PublishOutput);
}
