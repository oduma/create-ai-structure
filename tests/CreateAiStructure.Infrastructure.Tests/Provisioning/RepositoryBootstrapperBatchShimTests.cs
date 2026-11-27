using System.Diagnostics;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Workflow;
using CreateAiStructure.Infrastructure.Process;
using CreateAiStructure.Infrastructure.Provisioning;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Infrastructure.Tests.Provisioning;

/// <summary>
/// Proves the real bootstrapper argument lists (including every <see cref="WorkflowLabelCatalog"/> entry and the
/// initial commit message) pass <see cref="ProcessRunner"/>'s batch-file argument guard when <c>gh</c>, <c>git</c>
/// and <c>depviz</c> resolve to <c>.cmd</c> shims (common for npm/scoop installs on Windows).
/// </summary>
public class RepositoryBootstrapperBatchShimTests
{
    private static readonly string ShimDirectory = Path.Combine(Path.GetTempPath(), "car-batch-shim-tests", "bin");
    private static readonly string WorkingDirectory = Path.Combine(Path.GetTempPath(), "car-batch-shim-tests", "work");

    private readonly IExecutableResolver _resolver = Substitute.For<IExecutableResolver>();
    private readonly IProcessStarter _starter = Substitute.For<IProcessStarter>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly List<ProcessStartInfo> _started = [];

    public RepositoryBootstrapperBatchShimTests()
    {
        _resolver.Resolve(Arg.Any<string>()).Returns(call => Path.Combine(ShimDirectory, $"{call.Arg<string>()}.cmd"));
        _starter.Start(Arg.Do<ProcessStartInfo>(_started.Add)).Returns(_ => CompletedHandle("https://github.com/octo/my-repo.git\n"));
    }

    private static IProcessHandle CompletedHandle(string stdout)
    {
        var handle = Substitute.For<IProcessHandle>();
        handle.ReadStandardOutputToEndAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(stdout));
        handle.ReadStandardErrorToEndAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(string.Empty));
        handle.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        handle.HasExited.Returns(true);
        handle.ExitCode.Returns(0);
        return handle;
    }

    private RepositoryBootstrapper CreateSut() => new(new ProcessRunner(_resolver, _starter), _fileSystem);

    [Fact]
    public async Task Finalize_with_the_workflow_label_catalog_succeeds_against_cmd_shims()
    {
        _fileSystem.DirectoryExists(WorkingDirectory).Returns(true);

        var result = await CreateSut().FinalizeRepositoryAsync(WorkingDirectory, WorkflowLabelCatalog.All);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        _started.Should().HaveCount(WorkflowLabelCatalog.All.Count + 4);
        _started.Should().OnlyContain(psi => psi.FileName.EndsWith(".cmd", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_and_clone_succeeds_against_cmd_shims()
    {
        const string name = "my-repo";
        var repositoryDirectory = Path.Combine(WorkingDirectory, name);
        _fileSystem.DirectoryExists(WorkingDirectory).Returns(true);
        _fileSystem.DirectoryExists(repositoryDirectory).Returns(false, true);

        var result = await CreateSut().CreateAndCloneRepositoryAsync(RepositoryName.Create(name).Value, WorkingDirectory);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.RepositorySlug.Should().Be("octo/my-repo");
        _started.Should().HaveCount(3);
    }
}
