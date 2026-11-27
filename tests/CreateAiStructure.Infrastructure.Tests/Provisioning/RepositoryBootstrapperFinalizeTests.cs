using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Workflow;
using CreateAiStructure.Infrastructure.Provisioning;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CreateAiStructure.Infrastructure.Tests.Provisioning;

public class RepositoryBootstrapperFinalizeTests
{
    private const string CommitMessage = "chore: initialize AI workflow structure";

    private static readonly string RepositoryDirectory =
        Path.Combine(Path.GetTempPath(), "car-repository-bootstrapper-finalize-tests", "my-repo");

    private static readonly string[] DepvizArguments = ["init"];
    private static readonly string[] AddArguments = ["add", "--all"];
    private static readonly string[] CommitArguments = ["commit", "-m", CommitMessage];
    private static readonly string[] PushArguments = ["push", "-u", "origin", "main"];

    private static readonly string[] ExpectedLabelNamesInOrder =
    [
        "epic",
        "status:backlog",
        "status:in-progress",
        "stage:po-spec",
        "stage:ready-for-arch",
        "stage:implementing",
        "stage:qa",
        "stage:human-review",
    ];

    private static readonly ProcessExecutionResult Ok = new(0, string.Empty, string.Empty);

    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    // ----- helpers -------------------------------------------------------------------------

    private RepositoryBootstrapper CreateSut() => new(_runner, _fileSystem);

    private static IReadOnlyList<WorkflowLabelDefinition> Catalog => WorkflowLabelCatalog.All;

    private static string[] LabelArguments(WorkflowLabelDefinition label) =>
        ["label", "create", "--color", label.ColorHex, "--description", label.Description, "--force", "--", label.Name];

    private static bool Matches(ProcessExecutionRequest request, string fileName, string[] arguments) =>
        request.FileName == fileName
        && request.Arguments.SequenceEqual(arguments)
        && request.WorkingDirectory == RepositoryDirectory;

    private static ProcessExecutionRequest IsDepviz() =>
        Arg.Is<ProcessExecutionRequest>(r => Matches(r, "depviz", DepvizArguments));

    private static ProcessExecutionRequest IsLabel(WorkflowLabelDefinition label)
    {
        var arguments = LabelArguments(label);
        return Arg.Is<ProcessExecutionRequest>(r => Matches(r, "gh", arguments));
    }

    private static ProcessExecutionRequest IsAnyLabel() =>
        Arg.Is<ProcessExecutionRequest>(r => r.FileName == "gh");

    private static ProcessExecutionRequest IsAnyGit() =>
        Arg.Is<ProcessExecutionRequest>(r => r.FileName == "git");

    private static ProcessExecutionRequest IsAdd() =>
        Arg.Is<ProcessExecutionRequest>(r => Matches(r, "git", AddArguments));

    private static ProcessExecutionRequest IsCommit() =>
        Arg.Is<ProcessExecutionRequest>(r => Matches(r, "git", CommitArguments));

    private static ProcessExecutionRequest IsPush() =>
        Arg.Is<ProcessExecutionRequest>(r => Matches(r, "git", PushArguments));

    private void GivenDirectoryExists() =>
        _fileSystem.DirectoryExists(RepositoryDirectory).Returns(true);

    private void GivenEverythingSucceeds()
    {
        GivenDirectoryExists();
        _runner.RunAsync(Arg.Any<ProcessExecutionRequest>(), Arg.Any<CancellationToken>()).Returns(Ok);
    }

    private Task<Result> ActAsync(
        IReadOnlyList<WorkflowLabelDefinition>? labels = null,
        CancellationToken cancellationToken = default) =>
        CreateSut().FinalizeRepositoryAsync(RepositoryDirectory, labels ?? Catalog, cancellationToken);

    private void ShouldNotHaveRunAnything() =>
        _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);

    // ----- catalog sanity ------------------------------------------------------------------

    [Fact]
    public void Catalog_contains_the_eight_workflow_labels_in_state_machine_order()
    {
        Catalog.Select(l => l.Name).Should().Equal(ExpectedLabelNamesInOrder);
    }

    // ----- argument guards -----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Throws_when_repository_directory_is_null_empty_or_whitespace(string? repositoryDirectory)
    {
        var act = () => CreateSut().FinalizeRepositoryAsync(repositoryDirectory!, Catalog);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("repositoryDirectory");
        _fileSystem.ReceivedCalls().Should().BeEmpty();
        ShouldNotHaveRunAnything();
    }

    [Fact]
    public async Task Throws_when_labels_is_null()
    {
        var act = () => CreateSut().FinalizeRepositoryAsync(RepositoryDirectory, null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("labels");
        _fileSystem.ReceivedCalls().Should().BeEmpty();
        ShouldNotHaveRunAnything();
    }

    [Fact]
    public async Task Throws_when_labels_contains_a_null_element()
    {
        WorkflowLabelDefinition[] labels = [Catalog[0], null!, Catalog[1]];

        var act = () => CreateSut().FinalizeRepositoryAsync(RepositoryDirectory, labels);

        var assertion = await act.Should().ThrowAsync<ArgumentException>().WithParameterName("labels");
        assertion.Which.Should().NotBeOfType<ArgumentNullException>();
        _fileSystem.ReceivedCalls().Should().BeEmpty();
        ShouldNotHaveRunAnything();
    }

    [Fact]
    public async Task Throws_when_token_is_already_cancelled()
    {
        GivenEverythingSucceeds();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => ActAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _fileSystem.ReceivedCalls().Should().BeEmpty();
        ShouldNotHaveRunAnything();
    }

    // ----- pre-flight ----------------------------------------------------------------------

    [Fact]
    public async Task Fails_with_RepositoryDirectoryNotFound_when_repository_directory_is_missing()
    {
        _fileSystem.DirectoryExists(RepositoryDirectory).Returns(false);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.RepositoryDirectoryNotFound);
        result.Error.Code.Should().Be("Provisioning.RepositoryDirectoryNotFound");
        result.Error.Message.Should().Contain(RepositoryDirectory);
        _fileSystem.Received(1).DirectoryExists(RepositoryDirectory);
        ShouldNotHaveRunAnything();
    }

    // ----- happy path ----------------------------------------------------------------------

    [Fact]
    public async Task Runs_depviz_creates_all_catalog_labels_then_adds_commits_and_pushes_in_order()
    {
        GivenEverythingSucceeds();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var result = await ActAsync(Catalog, token);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _runner.RunAsync(IsDepviz(), token);
            foreach (var label in Catalog)
            {
                _runner.RunAsync(IsLabel(label), token);
            }

            _runner.RunAsync(IsAdd(), token);
            _runner.RunAsync(IsCommit(), token);
            _runner.RunAsync(IsPush(), token);
        });
        await _runner.ReceivedWithAnyArgs(12).RunAsync(default!, default);
        _fileSystem.Received(1).DirectoryExists(RepositoryDirectory);
    }

    [Fact]
    public async Task Happy_path_creates_the_eight_label_names_literally_in_order_with_catalog_color_and_description()
    {
        GivenDirectoryExists();
        var requests = new List<ProcessExecutionRequest>();
        _runner.RunAsync(Arg.Any<ProcessExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Ok)
            .AndDoes(call => requests.Add(call.Arg<ProcessExecutionRequest>()));

        var result = await ActAsync(Catalog);

        result.IsSuccess.Should().BeTrue();
        requests.Should().HaveCount(12);

        var labelRequests = requests.Skip(1).Take(8).ToList();
        labelRequests.Should().OnlyContain(r => r.FileName == "gh" && r.WorkingDirectory == RepositoryDirectory);
        labelRequests.Select(r => r.Arguments[^1]).Should().Equal(ExpectedLabelNamesInOrder);

        for (var i = 0; i < Catalog.Count; i++)
        {
            labelRequests[i].Arguments.Should().Equal(
                "label", "create", "--color", Catalog[i].ColorHex, "--description", Catalog[i].Description,
                "--force", "--", ExpectedLabelNamesInOrder[i]);
        }

        requests[0].FileName.Should().Be("depviz");
        requests[0].Arguments.Should().Equal("init");
        requests[9].Arguments.Should().Equal("add", "--all");
        requests[10].Arguments.Should().Equal("commit", "-m", "chore: initialize AI workflow structure");
        requests[11].Arguments.Should().Equal("push", "-u", "origin", "main");
        requests.Should().OnlyContain(r => r.WorkingDirectory == RepositoryDirectory);
    }

    [Fact]
    public async Task Passes_label_values_through_unchanged()
    {
        GivenEverythingSucceeds();
        var custom = WorkflowLabelDefinition.Create("custom label", "abcdef", "Some description with spaces").Value;

        var result = await ActAsync([custom]);

        result.IsSuccess.Should().BeTrue();
        await _runner.Received(1).RunAsync(
            Arg.Is<ProcessExecutionRequest>(r => Matches(
                r,
                "gh",
                new[] { "label", "create", "--color", "ABCDEF", "--description", "Some description with spaces", "--force", "--", "custom label" })),
            Arg.Any<CancellationToken>());
        await _runner.ReceivedWithAnyArgs(5).RunAsync(default!, default);
    }

    [Fact]
    public async Task Empty_labels_runs_only_depviz_add_commit_and_push()
    {
        GivenEverythingSucceeds();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var result = await ActAsync(Array.Empty<WorkflowLabelDefinition>(), token);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _runner.RunAsync(IsDepviz(), token);
            _runner.RunAsync(IsAdd(), token);
            _runner.RunAsync(IsCommit(), token);
            _runner.RunAsync(IsPush(), token);
        });
        await _runner.ReceivedWithAnyArgs(4).RunAsync(default!, default);
        await _runner.DidNotReceive().RunAsync(IsAnyLabel(), Arg.Any<CancellationToken>());
    }

    // ----- step failures -------------------------------------------------------------------

    [Fact]
    public async Task Fails_with_DepvizInitFailed_using_stderr_when_depviz_init_exits_non_zero()
    {
        GivenEverythingSucceeds();
        var failure = new ProcessExecutionResult(3, "ignored stdout", "  depviz: config error  \n");
        _runner.RunAsync(IsDepviz(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.DepvizInitFailed);
        result.Error.Code.Should().Be("Provisioning.DepvizInitFailed");
        result.Error.Message.Should().Be(SubprocessDiagnostics.Describe("depviz init", failure));
        result.Error.Message.Should().Contain("depviz init failed with exit code 3: depviz: config error");
        await _runner.ReceivedWithAnyArgs(1).RunAsync(default!, default);
        await _runner.DidNotReceive().RunAsync(IsAnyLabel(), Arg.Any<CancellationToken>());
        await _runner.DidNotReceive().RunAsync(IsAnyGit(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_LabelCreateFailed_using_stdout_when_first_label_fails()
    {
        GivenEverythingSucceeds();
        var first = Catalog[0];
        var failure = new ProcessExecutionResult(1, "  label stdout detail \n", "   ");
        _runner.RunAsync(IsLabel(first), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.LabelCreateFailed);
        result.Error.Code.Should().Be("Provisioning.LabelCreateFailed");
        result.Error.Message.Should().Be(SubprocessDiagnostics.Describe("gh label create for label 'epic'", failure));
        result.Error.Message.Should().Contain("epic");
        result.Error.Message.Should().Contain("label stdout detail");
        Received.InOrder(() =>
        {
            _runner.RunAsync(IsDepviz(), Arg.Any<CancellationToken>());
            _runner.RunAsync(IsLabel(first), Arg.Any<CancellationToken>());
        });
        await _runner.ReceivedWithAnyArgs(2).RunAsync(default!, default);
        foreach (var later in Catalog.Skip(1))
        {
            await _runner.DidNotReceive().RunAsync(IsLabel(later), Arg.Any<CancellationToken>());
        }

        await _runner.DidNotReceive().RunAsync(IsAnyGit(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_LabelCreateFailed_using_fallback_when_third_label_fails_and_halts_remaining_steps()
    {
        GivenEverythingSucceeds();
        var third = Catalog[2];
        var failure = new ProcessExecutionResult(4, string.Empty, string.Empty);
        _runner.RunAsync(IsLabel(third), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.LabelCreateFailed);
        result.Error.Code.Should().Be("Provisioning.LabelCreateFailed");
        result.Error.Message.Should().Be(
            SubprocessDiagnostics.Describe("gh label create for label 'status:in-progress'", failure));
        result.Error.Message.Should().Contain("status:in-progress");
        result.Error.Message.Should().Contain("No diagnostic output was captured.");
        Received.InOrder(() =>
        {
            _runner.RunAsync(IsDepviz(), Arg.Any<CancellationToken>());
            _runner.RunAsync(IsLabel(Catalog[0]), Arg.Any<CancellationToken>());
            _runner.RunAsync(IsLabel(Catalog[1]), Arg.Any<CancellationToken>());
            _runner.RunAsync(IsLabel(third), Arg.Any<CancellationToken>());
        });
        await _runner.ReceivedWithAnyArgs(4).RunAsync(default!, default);
        foreach (var later in Catalog.Skip(3))
        {
            await _runner.DidNotReceive().RunAsync(IsLabel(later), Arg.Any<CancellationToken>());
        }

        await _runner.DidNotReceive().RunAsync(IsAnyGit(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_GitAddFailed_when_git_add_exits_non_zero()
    {
        GivenEverythingSucceeds();
        var failure = new ProcessExecutionResult(128, string.Empty, "fatal: not a git repository\n");
        _runner.RunAsync(IsAdd(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.GitAddFailed);
        result.Error.Code.Should().Be("Provisioning.GitAddFailed");
        result.Error.Message.Should().Be(SubprocessDiagnostics.Describe("git add --all", failure));
        result.Error.Message.Should().Contain("git add --all failed with exit code 128: fatal: not a git repository");
        await _runner.ReceivedWithAnyArgs(10).RunAsync(default!, default);
        await _runner.DidNotReceive().RunAsync(IsCommit(), Arg.Any<CancellationToken>());
        await _runner.DidNotReceive().RunAsync(IsPush(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_GitCommitFailed_when_git_commit_exits_non_zero()
    {
        GivenEverythingSucceeds();
        var failure = new ProcessExecutionResult(1, "nothing to commit, working tree clean\n", string.Empty);
        _runner.RunAsync(IsCommit(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.GitCommitFailed);
        result.Error.Code.Should().Be("Provisioning.GitCommitFailed");
        result.Error.Message.Should().Be(SubprocessDiagnostics.Describe("git commit", failure));
        result.Error.Message.Should().Contain("git commit failed with exit code 1: nothing to commit, working tree clean");
        await _runner.ReceivedWithAnyArgs(11).RunAsync(default!, default);
        await _runner.DidNotReceive().RunAsync(IsPush(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_GitPushFailed_when_git_push_exits_non_zero()
    {
        GivenEverythingSucceeds();
        var failure = new ProcessExecutionResult(1, "push stdout", "error: failed to push some refs\n");
        _runner.RunAsync(IsPush(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await ActAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProvisioningErrorCodes.GitPushFailed);
        result.Error.Code.Should().Be("Provisioning.GitPushFailed");
        result.Error.Message.Should().Be(SubprocessDiagnostics.Describe("git push -u origin main", failure));
        result.Error.Message.Should().Contain("git push -u origin main failed with exit code 1: error: failed to push some refs");
        await _runner.ReceivedWithAnyArgs(12).RunAsync(default!, default);
    }

    // ----- cancellation --------------------------------------------------------------------

    [Fact]
    public async Task Propagates_OperationCanceledException_from_the_runner()
    {
        GivenEverythingSucceeds();
        _runner.RunAsync(IsLabel(Catalog[1]), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = () => ActAsync();

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _runner.DidNotReceive().RunAsync(IsLabel(Catalog[2]), Arg.Any<CancellationToken>());
        await _runner.DidNotReceive().RunAsync(IsAnyGit(), Arg.Any<CancellationToken>());
    }
}
