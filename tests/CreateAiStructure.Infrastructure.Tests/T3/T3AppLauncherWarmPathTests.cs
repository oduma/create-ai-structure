using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;
using NSubstitute;
using static CreateAiStructure.Infrastructure.Tests.T3.T3AppLauncherFixture;

namespace CreateAiStructure.Infrastructure.Tests.T3;

/// <summary>AC-5: the T3 Code app is already running and the initial <c>t3 app</c> succeeds.</summary>
public class T3AppLauncherWarmPathTests
{
    private readonly T3AppLauncherFixture _fixture = new();

    public T3AppLauncherWarmPathTests() => _fixture.SetupRunResults(Success());

    [Fact]
    public async Task LaunchAsync_WhenInitialT3AppSucceeds_ReturnsWarmOutcomeWithImportReminder()
    {
        var result = await _fixture.CreateSut().LaunchAsync(RepoDir);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new T3LaunchOutcome(false, T3AppLauncher.ImportReminderMessage));
    }

    [Fact]
    public async Task LaunchAsync_WhenInitialT3AppSucceeds_ReminderMentionsT3JsonAndT3Code()
    {
        var result = await _fixture.CreateSut().LaunchAsync(RepoDir);

        result.Value.StartedDesktopApp.Should().BeFalse();
        result.Value.ReminderMessage.Should().NotBeNullOrWhiteSpace();
        result.Value.ReminderMessage.Should().Contain("t3.json").And.Contain("T3 Code");
    }

    [Fact]
    public async Task LaunchAsync_RunsT3AppOnceInTheRepositoryDirectory()
    {
        await _fixture.CreateSut().LaunchAsync(RepoDir);

        _fixture.RunRequests.Should().ContainSingle();
        var request = _fixture.RunRequests[0];
        request.FileName.Should().Be("t3");
        request.Arguments.Should().Equal("app");
        request.WorkingDirectory.Should().Be(RepoDir);
    }

    [Fact]
    public async Task LaunchAsync_ChecksTheRepositoryDirectoryExists()
    {
        await _fixture.CreateSut().LaunchAsync(RepoDir);

        _fixture.FileSystem.Received(1).DirectoryExists(RepoDir);
    }

    [Fact]
    public async Task LaunchAsync_PassesTheCancellationTokenToTheProcessRunner()
    {
        using var cts = new CancellationTokenSource();

        await _fixture.CreateSut().LaunchAsync(RepoDir, cts.Token);

        _ = _fixture.ProcessRunner.Received(1).RunAsync(Arg.Any<ProcessExecutionRequest>(), cts.Token);
    }

    [Fact]
    public async Task LaunchAsync_WhenInitialT3AppSucceeds_DoesNotLocateStartOrDelay()
    {
        await _fixture.CreateSut().LaunchAsync(RepoDir);

        _fixture.Locator.DidNotReceiveWithAnyArgs().Locate();
        _fixture.DetachedProcessLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }
}
