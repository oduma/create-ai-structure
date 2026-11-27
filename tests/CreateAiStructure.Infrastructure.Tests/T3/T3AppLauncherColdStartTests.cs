using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;
using NSubstitute;
using static CreateAiStructure.Infrastructure.Tests.T3.T3AppLauncherFixture;

namespace CreateAiStructure.Infrastructure.Tests.T3;

/// <summary>AC-6: cold-start recovery (locate, start detached, bounded readiness retry).</summary>
public class T3AppLauncherColdStartTests
{
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ThreeSeconds = TimeSpan.FromSeconds(3);

    private readonly T3AppLauncherFixture _fixture = new();

    [Fact]
    public async Task LaunchAsync_WhenRetryOneSucceeds_ReturnsColdStartOutcomeWithImportReminder()
    {
        _fixture.SetupRunResults(PipeFailure(), Success());

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new T3LaunchOutcome(true, T3AppLauncher.ImportReminderMessage));
        result.Value.ReminderMessage.Should().Contain("t3.json").And.Contain("T3 Code");
    }

    [Fact]
    public async Task LaunchAsync_WhenRetryOneSucceeds_WaitsOnlyTheInitialStartupDelay()
    {
        _fixture.SetupRunResults(PipeFailure(), Success());

        await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        _fixture.Delays.Should().Equal(FiveSeconds);
        _fixture.RunRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task LaunchAsync_WhenPipeIsUnreachable_StartsTheLocatedExecutableOnceWithNoArguments()
    {
        _fixture.SetupRunResults(PipeFailure(), Success());

        await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        _fixture.Locator.Received(1).Locate();
        _fixture.DetachedProcessLauncher.Received(1).StartDetached(Arg.Is<ProcessExecutionRequest>(request =>
            request.FileName == ExePath
            && request.Arguments.Count == 0
            && request.WorkingDirectory == null));
    }

    [Fact]
    public async Task LaunchAsync_WhenPipeIsUnreachable_PerformsColdStartStepsInOrder()
    {
        _fixture.SetupRunResults(PipeFailure(), PipeFailure(), Success());

        await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        Received.InOrder(() =>
        {
            _fixture.FileSystem.DirectoryExists(RepoDir);
            _fixture.ProcessRunner.RunAsync(Arg.Any<ProcessExecutionRequest>(), Arg.Any<CancellationToken>());
            _fixture.Locator.Locate();
            _fixture.DetachedProcessLauncher.StartDetached(Arg.Any<ProcessExecutionRequest>());
            _fixture.DelayProvider.DelayAsync(FiveSeconds, Arg.Any<CancellationToken>());
            _fixture.ProcessRunner.RunAsync(Arg.Any<ProcessExecutionRequest>(), Arg.Any<CancellationToken>());
            _fixture.DelayProvider.DelayAsync(ThreeSeconds, Arg.Any<CancellationToken>());
            _fixture.ProcessRunner.RunAsync(Arg.Any<ProcessExecutionRequest>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task LaunchAsync_WhenLastRetrySucceeds_WaitsInitialDelayThenRetryIntervals()
    {
        _fixture.SetupRunResults(PipeFailure(), PipeFailure(), PipeFailure(), PipeFailure(), Success());

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new T3LaunchOutcome(true, T3AppLauncher.ImportReminderMessage));
        _fixture.Delays.Should().Equal(FiveSeconds, ThreeSeconds, ThreeSeconds, ThreeSeconds);
        _fixture.RunRequests.Should().HaveCount(5);
        _fixture.DetachedProcessLauncher.ReceivedWithAnyArgs(1).StartDetached(default!);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task LaunchAsync_WhenRetryNSucceeds_StopsWithoutExtraDelaysOrRuns(int successfulRetry)
    {
        ProcessExecutionResult[] results =
        [
            .. Enumerable.Repeat(PipeFailure(), successfulRetry),
            Success(),
            PipeFailure(),
        ];
        _fixture.SetupRunResults(results);

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsSuccess.Should().BeTrue();
        result.Value.StartedDesktopApp.Should().BeTrue();
        _fixture.RunRequests.Should().HaveCount(1 + successfulRetry);
        TimeSpan[] expectedDelays = [FiveSeconds, .. Enumerable.Repeat(ThreeSeconds, successfulRetry - 1)];
        _fixture.Delays.Should().Equal(expectedDelays);
    }

    [Fact]
    public async Task LaunchAsync_EveryRetryRunsT3AppInTheRepositoryDirectory()
    {
        _fixture.SetupRunResults(PipeFailure(), PipeFailure(), PipeFailure(), Success());

        await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        _fixture.RunRequests.Should().HaveCount(4).And.AllSatisfy(request =>
        {
            request.FileName.Should().Be("t3");
            request.Arguments.Should().Equal("app");
            request.WorkingDirectory.Should().Be(RepoDir);
        });
    }

    [Fact]
    public async Task LaunchAsync_PassesTheCancellationTokenToDelaysAndRetries()
    {
        using var cts = new CancellationTokenSource();
        _fixture.SetupRunResults(PipeFailure(), PipeFailure(), Success());

        await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir, cts.Token);

        _ = _fixture.DelayProvider.Received(2).DelayAsync(Arg.Any<TimeSpan>(), cts.Token);
        _ = _fixture.ProcessRunner.Received(3).RunAsync(Arg.Any<ProcessExecutionRequest>(), cts.Token);
    }

    [Fact]
    public async Task LaunchAsync_WhenPipeIsUnreachableOnlyInStandardOutput_StillColdStarts()
    {
        _fixture.SetupRunResults(new ProcessExecutionResult(1, "T3 Code is not running", "   "), Success());

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsSuccess.Should().BeTrue();
        result.Value.StartedDesktopApp.Should().BeTrue();
        _fixture.Locator.Received(1).Locate();
    }
}
