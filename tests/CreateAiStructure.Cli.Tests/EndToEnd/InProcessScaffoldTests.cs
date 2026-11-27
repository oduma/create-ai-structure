using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Cli.Composition;
using CreateAiStructure.Cli.ConsoleIo;
using CreateAiStructure.Cli.Tests.EndToEnd.TestSupport;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Domain.Workflow;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace CreateAiStructure.Cli.Tests.EndToEnd;

/// <summary>
/// AC-7: runs the real composition root against the real embedded templates and real disk I/O inside an isolated
/// temp sandbox. Only the process boundary (<see cref="IProcessRunner"/>, <see cref="IDetachedProcessLauncher"/>) and
/// the process-wide environment (<see cref="IEnvironmentProvider"/>) are replaced, so no network, git or GUI is used.
/// </summary>
public sealed class InProcessScaffoldTests : IDisposable
{
    private const string RepositoryName = "e2e-sample-repo";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly EndToEndSandbox _sandbox = new();
    private readonly ScriptedProcessRunner _processRunner = new();
    private readonly IDetachedProcessLauncher _detachedLauncher = Substitute.For<IDetachedProcessLauncher>();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _sandbox.Dispose();
    }

    public static TheoryData<string, int> ProjectTypes => new()
    {
        { "webApp", 24 },
        { "MAUI", 23 },
    };

    [Theory]
    [MemberData(nameof(ProjectTypes))]
    public async Task Scaffold_run_succeeds_end_to_end_and_reports_the_summary(string type, int expectedFileCount)
    {
        var manifest = ManifestFor(type);
        manifest.Files.Should().HaveCount(expectedFileCount, "the AC-7 file count must match the domain manifest");

        var exitCode = await RunCliAsync(type);

        exitCode.Should().Be(CliExitCodes.Success);
        _error.ToString().Should().BeEmpty();
        ReadLines(_out.ToString()).Should().Equal(
            $"Repository created at {RepositoryDirectory}",
            $"Materialized {expectedFileCount} files.",
            CliOutputFormatter.T3AlreadyRunningMessage,
            T3AppLauncher.ImportReminderMessage);
    }

    [Theory]
    [MemberData(nameof(ProjectTypes))]
    public async Task Scaffold_run_writes_exactly_the_manifest_files_rendered_without_bom_or_tokens(string type, int expectedFileCount)
    {
        var manifest = ManifestFor(type);

        await RunCliSuccessfullyAsync(type);

        var expectedPaths = manifest.Files.Select(ToRepositoryPath).ToList();
        Directory.EnumerateFiles(RepositoryDirectory, "*", SearchOption.AllDirectories)
            .Should().BeEquivalentTo(expectedPaths)
            .And.HaveCount(expectedFileCount);

        foreach (var path in expectedPaths)
        {
            var bytes = await File.ReadAllBytesAsync(path);
            bytes.Should().NotBeEmpty("'{0}' is a rendered template", path);
            bytes.Take(Utf8Bom.Length).Should().NotEqual(Utf8Bom, "'{0}' must be written as UTF-8 without a BOM", path);
            (await File.ReadAllTextAsync(path)).Should().NotContain("{{", "'{0}' must have every token resolved", path);
        }
    }

    [Theory]
    [MemberData(nameof(ProjectTypes))]
    public async Task Scaffold_run_renders_claude_md_with_the_repository_name_and_project_type_lists(string type, int expectedFileCount)
    {
        _ = expectedFileCount;
        var manifest = ManifestFor(type);
        var otherTypeOnlyFiles = ProjectType.All
            .Where(other => other != manifest.ProjectType)
            .SelectMany(other => TemplateManifest.For(other).Files)
            .Except(manifest.Files, StringComparer.Ordinal)
            .ToList();

        await RunCliSuccessfullyAsync(type);

        var claudeMd = (await File.ReadAllTextAsync(ToRepositoryPath("CLAUDE.md"))).ReplaceLineEndings("\n");
        claudeMd.Should().StartWith($"# {RepositoryName} - Master AI Agent Directives");
        claudeMd.Should().Contain($"You are working on {RepositoryName}.");
        claudeMd.Should().Contain(string.Join("\n", manifest.ConstitutionFiles));
        claudeMd.Should().Contain(string.Join("\n", manifest.AgentFiles));
        otherTypeOnlyFiles.Should().NotBeEmpty();
        foreach (var foreignFile in otherTypeOnlyFiles)
        {
            claudeMd.Should().NotContain(foreignFile, "'{0}' belongs to another project type", foreignFile);
        }
    }

    [Theory]
    [MemberData(nameof(ProjectTypes))]
    public async Task Scaffold_run_invokes_the_external_tools_in_pipeline_order_on_the_warm_path(string type, int expectedFileCount)
    {
        _ = expectedFileCount;

        await RunCliSuccessfullyAsync(type);

        var repo = RepositoryDirectory;
        List<string> expected =
        [
            Describe(_sandbox.WorkingDirectory, "gh", "repo", "create", "--public", "--clone", "--", RepositoryName),
            Describe(repo, "git", "branch", "-M", "main"),
            Describe(repo, "git", "remote", "get-url", "origin"),
            Describe(repo, "depviz", "init"),
        ];
        expected.AddRange(WorkflowLabelCatalog.All.Select(label => Describe(
            repo, "gh", "label", "create", "--color", label.ColorHex, "--description", label.Description, "--force", "--", label.Name)));
        expected.Add(Describe(repo, "git", "add", "--all"));
        expected.Add(Describe(repo, "git", "commit", "-m", "chore: initialize AI workflow structure"));
        expected.Add(Describe(repo, "git", "push", "-u", "origin", "main"));
        expected.Add(Describe(repo, "t3", "app"));

        _processRunner.Calls.Select(call => Describe(call.WorkingDirectory, call.FileName, [.. call.Arguments]))
            .Should().Equal(expected);
        _detachedLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
    }

    [Fact]
    public async Task Scaffold_run_cleans_up_its_sandbox_on_dispose()
    {
        await RunCliSuccessfullyAsync("webApp");
        Directory.Exists(RepositoryDirectory).Should().BeTrue();

        _sandbox.Dispose();

        Directory.Exists(_sandbox.Root).Should().BeFalse();
    }

    private string RepositoryDirectory => Path.Combine(_sandbox.WorkingDirectory, RepositoryName);

    private async Task<int> RunCliAsync(string type)
    {
        var services = new ServiceCollection().AddCreateAiStructureServices(new ConsoleStreams(_out, _error));
        services.Replace(ServiceDescriptor.Singleton<IProcessRunner>(_processRunner));
        services.Replace(ServiceDescriptor.Singleton(_detachedLauncher));
        services.Replace(ServiceDescriptor.Singleton<IEnvironmentProvider>(new SandboxEnvironmentProvider(_sandbox)));

        await using var provider = CliServiceProviderFactory.Build(services);
        var runner = provider.GetRequiredService<CliRunner>();

        return await runner.RunAsync(["--type", type, "--repo", RepositoryName], CancellationToken.None);
    }

    private async Task RunCliSuccessfullyAsync(string type)
    {
        var exitCode = await RunCliAsync(type);

        _error.ToString().Should().BeEmpty();
        exitCode.Should().Be(CliExitCodes.Success);
    }

    private static TemplateManifest ManifestFor(string type) => TemplateManifest.For(ProjectType.Parse(type).Value);

    private string ToRepositoryPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(RepositoryDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Describe(string? workingDirectory, string fileName, params string[] arguments) =>
        $"[{workingDirectory}] {fileName} {string.Join(" | ", arguments)}";

    private static string[] ReadLines(string text) =>
        text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
}
