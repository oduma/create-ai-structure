using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;
using NSubstitute;
using static CreateAiStructure.Infrastructure.Tests.T3.T3AppLauncherFixture;

namespace CreateAiStructure.Infrastructure.Tests.T3;

/// <summary>AC-7 and FR-6: fail-fast paths, propagated errors, retry exhaustion and diagnostics.</summary>
public class T3AppLauncherFailureTests
{
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ThreeSeconds = TimeSpan.FromSeconds(3);

    private readonly T3AppLauncherFixture _fixture = new();

    /// <summary>Non-pipe failure streams: (stderr, stdout, expected diagnostics, text that must be absent).</summary>
    public static TheoryData<string, string, string, string?> CommandFailedDiagnostics => new()
    {
        { "  error: unknown option '--foo'  \n", "usage: t3 app", "error: unknown option '--foo'", "usage: t3 app" },
        { "   ", "  usage: t3 app [options]  \r\n", "usage: t3 app [options]", null },
        { "", "\t", FallbackDiagnostics, null },
        { "", "", FallbackDiagnostics, null },
    };

    /// <summary>Pipe-unreachable final retry streams: (stderr, stdout, expected diagnostics, text that must be absent).</summary>
    public static TheoryData<string, string, string, string?> ReadinessTimeoutDiagnostics => new()
    {
        { "  " + PipeStderr + "  \n", "stdout noise", PipeStderr, "stdout noise" },
        { "  \n ", "  failed to connect to T3 Code  \r\n", "failed to connect to T3 Code", null },
    };

    public static TheoryData<string> StartErrorCodes => new()
    {
        "T3Launch.DesktopStartFailed",
        "T3Launch.InvalidExecutablePath",
    };

    [Fact]
    public async Task LaunchAsync_WhenInitialFailureIsNotPipeRelated_ReturnsCommandFailedWithExitCodeAndStderr()
    {
        const string rawStderr = "  \n error: unknown option '--foo'  \n";
        _fixture.SetupRunResults(NonPipeFailure(exitCode: 64, standardError: rawStderr));

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CommandFailedCode);
        result.Error.Message.Should()
            .Contain("64")
            .And.Contain("error: unknown option '--foo'")
            .And.Contain("t3 app")
            .And.Contain(RepoDir)
            .And.NotContain(rawStderr);
    }

    [Fact]
    public async Task LaunchAsync_WhenInitialFailureIsNotPipeRelated_DoesNotLocateStartOrDelay()
    {
        _fixture.SetupRunResults(NonPipeFailure());

        await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        _fixture.RunRequests.Should().ContainSingle();
        _fixture.Locator.DidNotReceiveWithAnyArgs().Locate();
        _fixture.DetachedProcessLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    [Theory]
    [MemberData(nameof(CommandFailedDiagnostics))]
    public async Task LaunchAsync_WhenCommandFails_UsesStderrThenStdoutThenFallbackDiagnostics(
        string standardError, string standardOutput, string expectedDiagnostics, string? absentText)
    {
        _fixture.SetupRunResults(new ProcessExecutionResult(3, standardOutput, standardError));

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.Error.Code.Should().Be(CommandFailedCode);
        result.Error.Message.Should().Contain("3").And.Contain(expectedDiagnostics);
        if (absentText is not null)
        {
            result.Error.Message.Should().NotContain(absentText);
        }
    }

    [Fact]
    public async Task LaunchAsync_WhenLocatorFails_ReturnsTheLocatorErrorWithoutStartingOrDelaying()
    {
        var locatorError = new DomainError("T3Launch.DesktopExecutableNotFound", "The T3 Code desktop executable could not be found.");
        _fixture.Locator.Locate().Returns(Result<string>.Failure(locatorError));
        _fixture.SetupRunResults(PipeFailure());

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(locatorError.Code);
        result.Error.Message.Should().Be(locatorError.Message);
        _fixture.RunRequests.Should().ContainSingle();
        _fixture.DetachedProcessLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    [Theory]
    [MemberData(nameof(StartErrorCodes))]
    public async Task LaunchAsync_WhenDetachedStartFails_ReturnsTheStartErrorWithoutDelayOrRetry(string errorCode)
    {
        var startError = new DomainError(errorCode, $"The process '{ExePath}' could not be started.");
        _fixture.DetachedProcessLauncher.StartDetached(Arg.Any<ProcessExecutionRequest>()).Returns(Result.Failure(startError));
        _fixture.SetupRunResults(PipeFailure(), Success());

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(startError.Code);
        result.Error.Message.Should().Be(startError.Message);
        _fixture.RunRequests.Should().ContainSingle();
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task LaunchAsync_WhenRetryKFailsWithNonPipeError_ReturnsCommandFailedImmediately(int failingRetry)
    {
        ProcessExecutionResult[] results =
        [
            .. Enumerable.Repeat(PipeFailure(), failingRetry),
            NonPipeFailure(exitCode: 7, standardError: "fatal: workspace locked"),
            Success(),
        ];
        _fixture.SetupRunResults(results);

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CommandFailedCode);
        result.Error.Message.Should().Contain("7").And.Contain("fatal: workspace locked");
        _fixture.RunRequests.Should().HaveCount(1 + failingRetry);
        _fixture.Delays.Should().HaveCount(failingRetry);
        _fixture.DetachedProcessLauncher.ReceivedWithAnyArgs(1).StartDetached(default!);
    }

    [Fact]
    public async Task LaunchAsync_WhenAllRetriesArePipeUnreachable_ReturnsReadinessTimeout()
    {
        _fixture.SetupRunResults(PipeFailure(exitCode: 1), PipeFailure(exitCode: 99));

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReadinessTimeoutCode);
        result.Error.Message.Should()
            .Contain(ExePath)
            .And.Contain("4")
            .And.Contain("99")
            .And.Contain(PipeStderr)
            .And.Contain("t3.json");
    }

    [Fact]
    public async Task LaunchAsync_WhenAllRetriesArePipeUnreachable_HasNoTrailingDelay()
    {
        _fixture.SetupRunResults(PipeFailure());

        await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        _fixture.Delays.Should().Equal(FiveSeconds, ThreeSeconds, ThreeSeconds, ThreeSeconds);
        _fixture.RunRequests.Should().HaveCount(5);
        _fixture.Locator.Received(1).Locate();
        _fixture.DetachedProcessLauncher.ReceivedWithAnyArgs(1).StartDetached(default!);
    }

    [Fact]
    public async Task LaunchAsync_WhenMaxRetryAttemptsIsOne_ExhaustsAfterASingleRetry()
    {
        _fixture.SetupRunResults(PipeFailure());

        var result = await _fixture.CreateSut(new T3AppLauncherOptions(FiveSeconds, ThreeSeconds, 1)).LaunchAsync(RepoDir);

        result.Error.Code.Should().Be(ReadinessTimeoutCode);
        _fixture.Delays.Should().Equal(FiveSeconds);
        _fixture.RunRequests.Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(ReadinessTimeoutDiagnostics))]
    public async Task LaunchAsync_WhenRetriesAreExhausted_UsesStderrThenStdoutDiagnosticsOfTheLastAttempt(
        string standardError, string standardOutput, string expectedDiagnostics, string? absentText)
    {
        _fixture.SetupRunResults(
            new ProcessExecutionResult(1, string.Empty, "first attempt: named pipe missing"),
            new ProcessExecutionResult(1, string.Empty, "first attempt: named pipe missing"),
            new ProcessExecutionResult(1, string.Empty, "first attempt: named pipe missing"),
            new ProcessExecutionResult(1, string.Empty, "first attempt: named pipe missing"),
            new ProcessExecutionResult(42, standardOutput, standardError));

        var result = await _fixture.CreateSut(CustomOptions).LaunchAsync(RepoDir);

        result.Error.Code.Should().Be(ReadinessTimeoutCode);
        result.Error.Message.Should()
            .Contain(ExePath)
            .And.Contain("42")
            .And.Contain(expectedDiagnostics)
            .And.NotContain("first attempt");
        if (absentText is not null)
        {
            result.Error.Message.Should().NotContain(absentText);
        }
    }
}
