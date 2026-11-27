using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Domain.Tooling;
using CreateAiStructure.Domain.Workflow;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CreateAiStructure.Application.Tests.Orchestration;

public class ScaffoldOrchestratorTests
{
    private const string MissingRequiredToolsCode = "Orchestration.MissingRequiredTools";
    private const string WorkingDirectory = @"C:\work";
    private const string RepositoryDirectory = @"C:\work\repo";
    private const string RepositorySlug = "owner/repo";
    private const string Reminder = "Import the repository in T3 Code.";

    private readonly IToolValidator _toolValidator = Substitute.For<IToolValidator>();
    private readonly IRepositoryBootstrapper _bootstrapper = Substitute.For<IRepositoryBootstrapper>();
    private readonly ITemplateMaterializer _materializer = Substitute.For<ITemplateMaterializer>();
    private readonly IT3AppLauncher _launcher = Substitute.For<IT3AppLauncher>();
    private readonly IReadOnlyList<string> _materializedFiles = [@"C:\work\repo\CLAUDE.md", @"C:\work\repo\AGENTS.md"];

    public static TheoryData<string, bool> ProjectTypesAndLaunchModes()
    {
        var data = new TheoryData<string, bool>();
        foreach (var projectType in ProjectType.All)
        {
            data.Add(projectType.CanonicalName, true);
            data.Add(projectType.CanonicalName, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ProjectTypesAndLaunchModes))]
    public async Task Happy_path_calls_every_port_once_in_order_with_exact_arguments(string projectTypeName, bool startedDesktopApp)
    {
        var projectType = ProjectType.Parse(projectTypeName).Value;
        var request = CreateRequest(projectType);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        TemplateManifest? capturedManifest = null;
        TemplateRenderContext? capturedContext = null;
        ArrangeHappyPath(startedDesktopApp);
        _materializer
            .MaterializeAsync(
                Arg.Do<TemplateManifest>(m => capturedManifest = m),
                Arg.Do<TemplateRenderContext>(c => capturedContext = c),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<string>>.Success(_materializedFiles)));

        var result = await CreateSut().ExecuteAsync(request, token);

        result.IsSuccess.Should().BeTrue();
        result.Value.RepositoryDirectory.Should().Be(RepositoryDirectory);
        result.Value.MaterializedFiles.Should().BeSameAs(_materializedFiles);
        result.Value.StartedT3DesktopApp.Should().Be(startedDesktopApp);
        result.Value.T3ImportReminder.Should().Be(Reminder);

        _ = _toolValidator.Received(1).ValidateRequiredToolsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        _ = _toolValidator.Received(1).ValidateRequiredToolsAsync(RequiredTools.All, token);
        _ = _bootstrapper.Received(1).CreateAndCloneRepositoryAsync(Arg.Any<RepositoryName>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = _bootstrapper.Received(1).CreateAndCloneRepositoryAsync(request.RepositoryName, WorkingDirectory, token);
        _ = _materializer.Received(1).MaterializeAsync(Arg.Any<TemplateManifest>(), Arg.Any<TemplateRenderContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = _materializer.Received(1).MaterializeAsync(Arg.Any<TemplateManifest>(), Arg.Any<TemplateRenderContext>(), RepositoryDirectory, token);
        _ = _bootstrapper.Received(1).FinalizeRepositoryAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<WorkflowLabelDefinition>>(), Arg.Any<CancellationToken>());
        _ = _bootstrapper.Received(1).FinalizeRepositoryAsync(RepositoryDirectory, WorkflowLabelCatalog.All, token);
        _ = _launcher.Received(1).LaunchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = _launcher.Received(1).LaunchAsync(RepositoryDirectory, token);

        var expectedManifest = TemplateManifest.For(projectType);
        capturedManifest.Should().NotBeNull();
        capturedManifest!.Should().BeEquivalentTo(expectedManifest);
        capturedContext.Should().NotBeNull();
        capturedContext!.RepositoryName.Should().Be(request.RepositoryName);
        capturedContext.RepositorySlug.Should().Be(RepositorySlug);
        capturedContext.ConstitutionFiles.Should().Equal(expectedManifest.ConstitutionFiles);
        capturedContext.AgentFiles.Should().Equal(expectedManifest.AgentFiles);

        Received.InOrder(() =>
        {
            _ = _toolValidator.ValidateRequiredToolsAsync(RequiredTools.All, token);
            _ = _bootstrapper.CreateAndCloneRepositoryAsync(request.RepositoryName, WorkingDirectory, token);
            _ = _materializer.MaterializeAsync(Arg.Any<TemplateManifest>(), Arg.Any<TemplateRenderContext>(), RepositoryDirectory, token);
            _ = _bootstrapper.FinalizeRepositoryAsync(RepositoryDirectory, WorkflowLabelCatalog.All, token);
            _ = _launcher.LaunchAsync(RepositoryDirectory, token);
        });
    }

    [Fact]
    public async Task Default_cancellation_token_is_forwarded_to_every_port()
    {
        var request = CreateRequest(ProjectType.Console);
        ArrangeHappyPath(startedDesktopApp: true);

        var result = await CreateSut().ExecuteAsync(request);

        result.IsSuccess.Should().BeTrue();
        _ = _toolValidator.Received(1).ValidateRequiredToolsAsync(RequiredTools.All, CancellationToken.None);
        _ = _bootstrapper.Received(1).CreateAndCloneRepositoryAsync(request.RepositoryName, WorkingDirectory, CancellationToken.None);
        _ = _materializer.Received(1).MaterializeAsync(Arg.Any<TemplateManifest>(), Arg.Any<TemplateRenderContext>(), RepositoryDirectory, CancellationToken.None);
        _ = _bootstrapper.Received(1).FinalizeRepositoryAsync(RepositoryDirectory, WorkflowLabelCatalog.All, CancellationToken.None);
        _ = _launcher.Received(1).LaunchAsync(RepositoryDirectory, CancellationToken.None);
    }

    [Theory]
    [InlineData(new[] { "gh" }, "gh")]
    [InlineData(new[] { "git", "depviz", "t3" }, "git, depviz, t3")]
    [InlineData(new[] { "t3", "git" }, "t3, git")]
    public async Task Missing_tools_fail_fast_with_a_message_listing_them_in_returned_order(string[] missingTools, string expectedList)
    {
        ArrangeHappyPath(startedDesktopApp: true);
        _toolValidator
            .ValidateRequiredToolsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ToolValidationResult(["found"], missingTools)));

        var result = await CreateSut().ExecuteAsync(CreateRequest(ProjectType.Api));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MissingRequiredToolsCode);
        result.Error.Message.Should().Be(
            $"Missing required tools on PATH: {expectedList}. Install them and ensure they are available on PATH before retrying.");
        _ = _bootstrapper.DidNotReceiveWithAnyArgs().CreateAndCloneRepositoryAsync(default!, default!, default);
        _ = _bootstrapper.DidNotReceiveWithAnyArgs().FinalizeRepositoryAsync(default!, default!, default);
        _ = _materializer.DidNotReceiveWithAnyArgs().MaterializeAsync(default!, default!, default!, default);
        _ = _launcher.DidNotReceiveWithAnyArgs().LaunchAsync(default!, default);
    }

    [Fact]
    public async Task Clone_failure_is_returned_unchanged_and_no_later_port_is_called()
    {
        ArrangeHappyPath(startedDesktopApp: true);
        var error = new DomainError("Provisioning.CloneFailed", "clone failed");
        _bootstrapper
            .CreateAndCloneRepositoryAsync(Arg.Any<RepositoryName>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<RepositoryCloneResult>.Failure(error)));

        var result = await CreateSut().ExecuteAsync(CreateRequest(ProjectType.WebApp));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
        _ = _materializer.DidNotReceiveWithAnyArgs().MaterializeAsync(default!, default!, default!, default);
        _ = _bootstrapper.DidNotReceiveWithAnyArgs().FinalizeRepositoryAsync(default!, default!, default);
        _ = _launcher.DidNotReceiveWithAnyArgs().LaunchAsync(default!, default);
    }

    [Fact]
    public async Task Materialize_failure_is_returned_unchanged_and_no_later_port_is_called()
    {
        ArrangeHappyPath(startedDesktopApp: true);
        var error = new DomainError("Templates.WriteFailed", "write failed");
        _materializer
            .MaterializeAsync(Arg.Any<TemplateManifest>(), Arg.Any<TemplateRenderContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<string>>.Failure(error)));

        var result = await CreateSut().ExecuteAsync(CreateRequest(ProjectType.Maui));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
        _ = _bootstrapper.DidNotReceiveWithAnyArgs().FinalizeRepositoryAsync(default!, default!, default);
        _ = _launcher.DidNotReceiveWithAnyArgs().LaunchAsync(default!, default);
    }

    [Fact]
    public async Task Finalize_failure_is_returned_unchanged_and_launcher_is_not_called()
    {
        ArrangeHappyPath(startedDesktopApp: true);
        var error = new DomainError("Provisioning.FinalizeFailed", "finalize failed");
        _bootstrapper
            .FinalizeRepositoryAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<WorkflowLabelDefinition>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure(error)));

        var result = await CreateSut().ExecuteAsync(CreateRequest(ProjectType.Console));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
        _ = _launcher.DidNotReceiveWithAnyArgs().LaunchAsync(default!, default);
    }

    [Fact]
    public async Task Launch_failure_is_returned_unchanged()
    {
        ArrangeHappyPath(startedDesktopApp: true);
        var error = new DomainError("Launching.Failed", "launch failed");
        _launcher
            .LaunchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<T3LaunchOutcome>.Failure(error)));

        var result = await CreateSut().ExecuteAsync(CreateRequest(ProjectType.Api));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
        _ = _launcher.Received(1).LaunchAsync(RepositoryDirectory, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_rejects_null_tool_validator()
    {
        var act = () => new ScaffoldOrchestrator(null!, _bootstrapper, _materializer, _launcher);

        act.Should().Throw<ArgumentNullException>().WithParameterName("toolValidator");
    }

    [Fact]
    public void Constructor_rejects_null_repository_bootstrapper()
    {
        var act = () => new ScaffoldOrchestrator(_toolValidator, null!, _materializer, _launcher);

        act.Should().Throw<ArgumentNullException>().WithParameterName("repositoryBootstrapper");
    }

    [Fact]
    public void Constructor_rejects_null_template_materializer()
    {
        var act = () => new ScaffoldOrchestrator(_toolValidator, _bootstrapper, null!, _launcher);

        act.Should().Throw<ArgumentNullException>().WithParameterName("templateMaterializer");
    }

    [Fact]
    public void Constructor_rejects_null_t3_app_launcher()
    {
        var act = () => new ScaffoldOrchestrator(_toolValidator, _bootstrapper, _materializer, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("t3AppLauncher");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_null_request_without_calling_any_port()
    {
        var act = () => CreateSut().ExecuteAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
        AssertNoPortCalls();
    }

    [Fact]
    public async Task Pre_cancelled_token_throws_without_calling_any_port()
    {
        ArrangeHappyPath(startedDesktopApp: true);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => CreateSut().ExecuteAsync(CreateRequest(ProjectType.Console), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        AssertNoPortCalls();
    }

    [Fact]
    public async Task Cancellation_thrown_by_a_port_mid_pipeline_propagates_and_later_ports_are_not_called()
    {
        ArrangeHappyPath(startedDesktopApp: true);
        var cancelled = new OperationCanceledException();
        _materializer
            .MaterializeAsync(Arg.Any<TemplateManifest>(), Arg.Any<TemplateRenderContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(cancelled);

        var act = () => CreateSut().ExecuteAsync(CreateRequest(ProjectType.WebApp));

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.Should().BeSameAs(cancelled);
        _ = _toolValidator.Received(1).ValidateRequiredToolsAsync(RequiredTools.All, Arg.Any<CancellationToken>());
        _ = _bootstrapper.Received(1).CreateAndCloneRepositoryAsync(Arg.Any<RepositoryName>(), WorkingDirectory, Arg.Any<CancellationToken>());
        _ = _bootstrapper.DidNotReceiveWithAnyArgs().FinalizeRepositoryAsync(default!, default!, default);
        _ = _launcher.DidNotReceiveWithAnyArgs().LaunchAsync(default!, default);
    }

    private ScaffoldOrchestrator CreateSut() => new(_toolValidator, _bootstrapper, _materializer, _launcher);

    private static ScaffoldRequest CreateRequest(ProjectType projectType) =>
        ScaffoldRequest.Create(projectType, RepositoryName.Create("repo").Value, WorkingDirectory).Value;

    private void ArrangeHappyPath(bool startedDesktopApp)
    {
        _toolValidator
            .ValidateRequiredToolsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ToolValidationResult(RequiredTools.All, [])));
        _bootstrapper
            .CreateAndCloneRepositoryAsync(Arg.Any<RepositoryName>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<RepositoryCloneResult>.Success(new RepositoryCloneResult(RepositoryDirectory, RepositorySlug))));
        _materializer
            .MaterializeAsync(Arg.Any<TemplateManifest>(), Arg.Any<TemplateRenderContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<string>>.Success(_materializedFiles)));
        _bootstrapper
            .FinalizeRepositoryAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<WorkflowLabelDefinition>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        _launcher
            .LaunchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<T3LaunchOutcome>.Success(new T3LaunchOutcome(startedDesktopApp, Reminder))));
    }

    private void AssertNoPortCalls()
    {
        _toolValidator.ReceivedCalls().Should().BeEmpty();
        _bootstrapper.ReceivedCalls().Should().BeEmpty();
        _materializer.ReceivedCalls().Should().BeEmpty();
        _launcher.ReceivedCalls().Should().BeEmpty();
    }
}
