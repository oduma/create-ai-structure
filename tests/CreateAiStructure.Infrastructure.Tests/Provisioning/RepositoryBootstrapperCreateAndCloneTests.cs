using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Infrastructure.Provisioning;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CreateAiStructure.Infrastructure.Tests.Provisioning;

public class RepositoryBootstrapperCreateAndCloneTests
{
    private const string Name = "my-repo";
    private const string RemoteUrl = "https://github.com/octo/my-repo.git\n";
    private const string ExpectedSlug = "octo/my-repo";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "car-repository-bootstrapper-tests");
    private static readonly string WorkingDirectory = Path.Combine(Root, "work");
    private static readonly string RepositoryDirectory = Path.Combine(WorkingDirectory, Name);

    private static readonly string[] CreateArguments = ["repo", "create", "--public", "--clone", "--", Name];
    private static readonly string[] BranchArguments = ["branch", "-M", "main"];
    private static readonly string[] RemoteArguments = ["remote", "get-url", "origin"];

    private static readonly ProcessExecutionResult Ok = new(0, string.Empty, string.Empty);

    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    // ----- helpers -------------------------------------------------------------------------

    private RepositoryBootstrapper CreateSut() => new(_runner, _fileSystem);

    private static RepositoryName RepoName() => RepositoryName.Create(Name).Value;

    private static bool Matches(ProcessExecutionRequest request, string fileName, string[] arguments, string workingDirectory) =>
        request.FileName == fileName
        && request.Arguments.SequenceEqual(arguments)
        && request.WorkingDirectory == workingDirectory;

    private static ProcessExecutionRequest IsCreate() =>
        Arg.Is<ProcessExecutionRequest>(r => Matches(r, "gh", CreateArguments, WorkingDirectory));

    private static ProcessExecutionRequest IsBranch() =>
        Arg.Is<ProcessExecutionRequest>(r => Matches(r, "git", BranchArguments, RepositoryDirectory));

    private static ProcessExecutionRequest IsRemote() =>
        Arg.Is<ProcessExecutionRequest>(r => Matches(r, "git", RemoteArguments, RepositoryDirectory));

    private void GivenPreFlightPasses()
    {
        _fileSystem.DirectoryExists(WorkingDirectory).Returns(true);
        _fileSystem.DirectoryExists(RepositoryDirectory).Returns(false, true);
        _fileSystem.FileExists(RepositoryDirectory).Returns(false);
    }

    private void GivenCreate(ProcessExecutionResult result) =>
        _runner.RunAsync(IsCreate(), Arg.Any<CancellationToken>()).Returns(result);

    private void GivenBranch(ProcessExecutionResult result) =>
        _runner.RunAsync(IsBranch(), Arg.Any<CancellationToken>()).Returns(result);

    private void GivenRemote(ProcessExecutionResult result) =>
        _runner.RunAsync(IsRemote(), Arg.Any<CancellationToken>()).Returns(result);

    private void GivenEverythingSucceeds()
    {
        GivenPreFlightPasses();
        GivenCreate(Ok);
        GivenBranch(Ok);
        GivenRemote(new ProcessExecutionResult(0, RemoteUrl, string.Empty));
    }

    private Task<Result<RepositoryCloneResult>> ActAsync(CancellationToken cancellationToken = default) =>
        CreateSut().CreateAndCloneRepositoryAsync(RepoName(), WorkingDirectory, cancellationToken);

    private void ShouldNotHaveRunAnything() =>
        _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);

    // ----- constructor guards --------------------------------------------------------------

    [Fact]
    public void Ctor_throws_when_process_runner_is_null()
    {
        var act = () => new RepositoryBootstrapper(null!, _fileSystem);

        act.Should().Throw<ArgumentNullException>().WithParameterName("processRunner");
    }

    [Fact]
    public void Ctor_throws_when_file_system_is_null()
    {
        var act = () => new RepositoryBootstrapper(_runner, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    [Fact]
    public void Implements_the_application_port()
    {
        CreateSut().Should().BeAssignableTo<IRepositoryBootstrapper>();
    }

    // ----- argument guards -----------------------------------------------------------------

    [Fact]
    public async Task Throws_when_repository_name_is_null()
    {
        var act = () => CreateSut().CreateAndCloneRepositoryAsync(null!, WorkingDirectory);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("repositoryName");
        _fileSystem.ReceivedCalls().Should().BeEmpty();
        ShouldNotHaveRunAnything();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Throws_when_working_directory_is_null_empty_or_whitespace(string? workingDirectory)
    {
        var act = () => CreateSut().CreateAndCloneRepositoryAsync(RepoName(), workingDirectory!);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("workingDirectory");
        _fileSystem.ReceivedCalls().Should().BeEmpty();
        ShouldNotHaveRunAnything();
    }

    [Fact]
    public async Task Throws_when_token_is_already_cancelled()
    {
        GivenEverythingSucceeds();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => ActAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _fileSystem.ReceivedCalls().Should().BeEmpty();
        ShouldNotHaveRunAnything();
    }

    // ----- pre-flight ----------------------------------------------------------------------

    [Fact]
    public async Task Fails_with_WorkingDirectoryNotFound_when_working_directory_is_missing()
    {
        _fileSystem.DirectoryExists(WorkingDirectory).Returns(false);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.WorkingDirectoryNotFound);
        result.Error.Code.Should().Be("Provisioning.WorkingDirectoryNotFound");
        result.Error.Message.Should().Contain(WorkingDirectory);
        ShouldNotHaveRunAnything();
    }

    [Fact]
    public async Task Fails_with_TargetDirectoryAlreadyExists_when_target_is_an_existing_directory()
    {
        _fileSystem.DirectoryExists(WorkingDirectory).Returns(true);
        _fileSystem.DirectoryExists(RepositoryDirectory).Returns(true);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.TargetDirectoryAlreadyExists);
        result.Error.Code.Should().Be("Provisioning.TargetDirectoryAlreadyExists");
        result.Error.Message.Should().Contain(RepositoryDirectory);
        ShouldNotHaveRunAnything();
    }

    [Fact]
    public async Task Fails_with_TargetDirectoryAlreadyExists_when_target_is_an_existing_file()
    {
        _fileSystem.DirectoryExists(WorkingDirectory).Returns(true);
        _fileSystem.DirectoryExists(RepositoryDirectory).Returns(false);
        _fileSystem.FileExists(RepositoryDirectory).Returns(true);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.TargetDirectoryAlreadyExists);
        result.Error.Code.Should().Be("Provisioning.TargetDirectoryAlreadyExists");
        result.Error.Message.Should().Contain(RepositoryDirectory);
        ShouldNotHaveRunAnything();
    }

    // ----- step failures -------------------------------------------------------------------

    [Fact]
    public async Task Fails_with_RepositoryCreateFailed_when_gh_repo_create_exits_non_zero()
    {
        GivenPreFlightPasses();
        GivenCreate(new ProcessExecutionResult(1, "ignored stdout", "  HTTP 422: name already exists  \n"));

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.RepositoryCreateFailed);
        result.Error.Code.Should().Be("Provisioning.RepositoryCreateFailed");
        result.Error.Message.Should().Contain("gh repo create failed with exit code 1: HTTP 422: name already exists");
        await _runner.Received(1).RunAsync(IsCreate(), Arg.Any<CancellationToken>());
        await _runner.ReceivedWithAnyArgs(1).RunAsync(default!, default);
        await _runner.DidNotReceive().RunAsync(
            Arg.Is<ProcessExecutionRequest>(r => r.FileName == "git"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_CloneDirectoryNotFound_when_clone_directory_is_missing_after_create()
    {
        _fileSystem.DirectoryExists(WorkingDirectory).Returns(true);
        _fileSystem.DirectoryExists(RepositoryDirectory).Returns(false, false);
        GivenCreate(Ok);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.CloneDirectoryNotFound);
        result.Error.Code.Should().Be("Provisioning.CloneDirectoryNotFound");
        result.Error.Message.Should().Contain(RepositoryDirectory);
        _fileSystem.Received(2).DirectoryExists(RepositoryDirectory);
        await _runner.Received(1).RunAsync(IsCreate(), Arg.Any<CancellationToken>());
        await _runner.ReceivedWithAnyArgs(1).RunAsync(default!, default);
    }

    [Fact]
    public async Task Fails_with_DefaultBranchSetupFailed_when_git_branch_exits_non_zero()
    {
        GivenPreFlightPasses();
        GivenCreate(Ok);
        GivenBranch(new ProcessExecutionResult(128, "branch output", string.Empty));

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.DefaultBranchSetupFailed);
        result.Error.Code.Should().Be("Provisioning.DefaultBranchSetupFailed");
        result.Error.Message.Should().Contain("git branch -M main failed with exit code 128: branch output");
        Received.InOrder(() =>
        {
            _runner.RunAsync(IsCreate(), Arg.Any<CancellationToken>());
            _runner.RunAsync(IsBranch(), Arg.Any<CancellationToken>());
        });
        await _runner.ReceivedWithAnyArgs(2).RunAsync(default!, default);
        await _runner.DidNotReceive().RunAsync(IsRemote(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_RemoteOriginLookupFailed_when_git_remote_get_url_exits_non_zero()
    {
        GivenPreFlightPasses();
        GivenCreate(Ok);
        GivenBranch(Ok);
        GivenRemote(new ProcessExecutionResult(2, string.Empty, string.Empty));

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.RemoteOriginLookupFailed);
        result.Error.Code.Should().Be("Provisioning.RemoteOriginLookupFailed");
        result.Error.Message.Should().Contain(
            "git remote get-url origin failed with exit code 2: No diagnostic output was captured.");
        Received.InOrder(() =>
        {
            _runner.RunAsync(IsCreate(), Arg.Any<CancellationToken>());
            _runner.RunAsync(IsBranch(), Arg.Any<CancellationToken>());
            _runner.RunAsync(IsRemote(), Arg.Any<CancellationToken>());
        });
        await _runner.ReceivedWithAnyArgs(3).RunAsync(default!, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \r\n ")]
    public async Task Fails_with_RemoteOriginInvalid_and_empty_placeholder_when_remote_output_is_blank(string output)
    {
        GivenPreFlightPasses();
        GivenCreate(Ok);
        GivenBranch(Ok);
        GivenRemote(new ProcessExecutionResult(0, output, string.Empty));

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.RemoteOriginInvalid);
        result.Error.Code.Should().Be("Provisioning.RemoteOriginInvalid");
        result.Error.Message.Should().Contain("<empty>");
        await _runner.ReceivedWithAnyArgs(3).RunAsync(default!, default);
    }

    [Fact]
    public async Task Fails_with_RemoteOriginInvalid_containing_raw_value_when_remote_output_is_garbage()
    {
        GivenPreFlightPasses();
        GivenCreate(Ok);
        GivenBranch(Ok);
        GivenRemote(new ProcessExecutionResult(0, "not-a-remote-url\n", string.Empty));

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.RemoteOriginInvalid);
        result.Error.Code.Should().Be("Provisioning.RemoteOriginInvalid");
        result.Error.Message.Should().Contain("not-a-remote-url");
        result.Error.Message.Should().NotContain("<empty>");
    }

    // ----- happy path ----------------------------------------------------------------------

    [Fact]
    public async Task Creates_clones_normalises_branch_and_returns_directory_and_slug()
    {
        GivenEverythingSucceeds();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var result = await ActAsync(token);

        result.IsSuccess.Should().BeTrue();
        result.Value.RepositoryDirectory.Should().Be(RepositoryDirectory);
        result.Value.RepositorySlug.Should().Be(ExpectedSlug);
        Received.InOrder(() =>
        {
            _runner.RunAsync(IsCreate(), token);
            _runner.RunAsync(IsBranch(), token);
            _runner.RunAsync(IsRemote(), token);
        });
        await _runner.ReceivedWithAnyArgs(3).RunAsync(default!, default);
        _fileSystem.Received(1).DirectoryExists(WorkingDirectory);
        _fileSystem.Received(2).DirectoryExists(RepositoryDirectory);
        _fileSystem.Received(1).FileExists(RepositoryDirectory);
    }

    [Fact]
    public async Task Resolves_slug_from_scp_style_remote()
    {
        GivenPreFlightPasses();
        GivenCreate(Ok);
        GivenBranch(Ok);
        GivenRemote(new ProcessExecutionResult(0, "git@github.com:octo/my-repo.git\n", string.Empty));

        var result = await ActAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.RepositorySlug.Should().Be(ExpectedSlug);
    }

    // ----- cancellation --------------------------------------------------------------------

    [Fact]
    public async Task Propagates_OperationCanceledException_from_the_runner()
    {
        GivenPreFlightPasses();
        GivenCreate(Ok);
        _runner.RunAsync(IsBranch(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = () => ActAsync();

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _runner.DidNotReceive().RunAsync(IsRemote(), Arg.Any<CancellationToken>());
    }
}
