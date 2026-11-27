using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using FluentAssertions;
using NSubstitute;
using static CreateAiStructure.Infrastructure.Tests.T3.T3AppLauncherFixture;

namespace CreateAiStructure.Infrastructure.Tests.T3;

/// <summary>FR-5.6: <see cref="OperationCanceledException"/> always propagates and is never turned into a result.</summary>
public class T3AppLauncherCancellationTests
{
    private readonly T3AppLauncherFixture _fixture = new();

    [Fact]
    public async Task LaunchAsync_WhenInitialRunIsCancelled_PropagatesOperationCanceledException()
    {
        _fixture.SetupRunSteps(() => throw new OperationCanceledException());
        var sut = _fixture.CreateSut(CustomOptions);

        var act = () => sut.LaunchAsync(RepoDir);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _fixture.Locator.DidNotReceiveWithAnyArgs().Locate();
        _fixture.DetachedProcessLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    [Fact]
    public async Task LaunchAsync_WhenTokenIsCancelledDuringLocate_ThrowsBeforeStartingTheDesktopApp()
    {
        using var cts = new CancellationTokenSource();
        _fixture.SetupRunResults(PipeFailure(), Success());
        _fixture.Locator.Locate().Returns(_ =>
        {
            cts.Cancel();
            return Result<string>.Success(ExePath);
        });
        var sut = _fixture.CreateSut(CustomOptions);

        var act = () => sut.LaunchAsync(RepoDir, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _fixture.Locator.Received(1).Locate();
        _fixture.DetachedProcessLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
        _fixture.RunRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task LaunchAsync_WhenDelayIsCancelled_PropagatesOperationCanceledExceptionWithoutRetrying()
    {
        _fixture.SetupRunResults(PipeFailure(), Success());
        _fixture.DelayProvider
            .DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new OperationCanceledException()));
        var sut = _fixture.CreateSut(CustomOptions);

        var act = () => sut.LaunchAsync(RepoDir);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _fixture.DetachedProcessLauncher.ReceivedWithAnyArgs(1).StartDetached(default!);
        _fixture.RunRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task LaunchAsync_WhenRetryRunIsCancelled_PropagatesOperationCanceledExceptionWithoutFurtherRetries()
    {
        _fixture.SetupRunSteps(
            () => PipeFailure(),
            () => throw new OperationCanceledException());
        var sut = _fixture.CreateSut(CustomOptions);

        var act = () => sut.LaunchAsync(RepoDir);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _fixture.RunRequests.Should().HaveCount(2);
        _fixture.Delays.Should().ContainSingle();
    }

    [Fact]
    public async Task LaunchAsync_WhenRetryRunReturnsACancelledTask_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var calls = 0;
        _fixture.ProcessRunner
            .RunAsync(Arg.Any<ProcessExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? Task.FromResult(PipeFailure())
                : Task.FromCanceled<ProcessExecutionResult>(cts.Token));
        var sut = _fixture.CreateSut(CustomOptions);

        var act = () => sut.LaunchAsync(RepoDir);

        await act.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(2);
    }
}
