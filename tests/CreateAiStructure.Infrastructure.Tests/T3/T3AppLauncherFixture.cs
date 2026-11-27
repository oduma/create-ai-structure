using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Infrastructure.T3;
using NSubstitute;

namespace CreateAiStructure.Infrastructure.Tests.T3;

/// <summary>
/// Shared substitutes, canned process results and call recordings for <see cref="T3AppLauncher"/> tests.
/// By default the repository directory exists, the locator finds <see cref="ExePath"/>, the detached start
/// succeeds and every delay completes immediately (recorded in <see cref="Delays"/>).
/// </summary>
internal sealed class T3AppLauncherFixture
{
    public const string RepoDir = @"C:\repos\target-repo";
    public const string ExePath = @"C:\Users\u\AppData\Local\Programs\t3code\T3 Code.exe";
    public const string PipeStderr = @"Error: connect ENOENT \\.\pipe\t3code";
    public const string NonPipeStderr = "error: unknown option '--foo'";
    public const string FallbackDiagnostics = "No diagnostic output was captured.";

    public const string RepositoryDirectoryNotFoundCode = "T3Launch.RepositoryDirectoryNotFound";
    public const string CommandFailedCode = "T3Launch.CommandFailed";
    public const string ReadinessTimeoutCode = "T3Launch.ReadinessTimeout";

    private readonly Queue<Func<ProcessExecutionResult>> _runSteps = new();
    private Func<ProcessExecutionResult>? _lastRunStep;

    public T3AppLauncherFixture()
    {
        FileSystem.DirectoryExists(RepoDir).Returns(true);
        Locator.Locate().Returns(Result<string>.Success(ExePath));
        DetachedProcessLauncher.StartDetached(Arg.Any<ProcessExecutionRequest>()).Returns(Result.Success());
        DelayProvider
            .DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Delays.Add(callInfo.ArgAt<TimeSpan>(0));
                return Task.CompletedTask;
            });
        ProcessRunner
            .RunAsync(Arg.Any<ProcessExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                RunRequests.Add(callInfo.ArgAt<ProcessExecutionRequest>(0));
                var step = _runSteps.Count > 0 ? _runSteps.Dequeue() : _lastRunStep;
                _lastRunStep = step;
                return Task.FromResult(step!());
            });
    }

    public IProcessRunner ProcessRunner { get; } = Substitute.For<IProcessRunner>();

    public IDetachedProcessLauncher DetachedProcessLauncher { get; } = Substitute.For<IDetachedProcessLauncher>();

    public IFileSystem FileSystem { get; } = Substitute.For<IFileSystem>();

    public IT3DesktopExecutableLocator Locator { get; } = Substitute.For<IT3DesktopExecutableLocator>();

    public IDelayProvider DelayProvider { get; } = Substitute.For<IDelayProvider>();

    /// <summary>Gets every request passed to <see cref="IProcessRunner.RunAsync"/>, in call order.</summary>
    public List<ProcessExecutionRequest> RunRequests { get; } = [];

    /// <summary>Gets every delay passed to <see cref="IDelayProvider.DelayAsync"/>, in call order.</summary>
    public List<TimeSpan> Delays { get; } = [];

    /// <summary>The custom options used by most cold-start tests: 5s initial, 3s interval, 4 retries.</summary>
    public static T3AppLauncherOptions CustomOptions =>
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3), 4);

    public static ProcessExecutionResult Success() => new(0, "T3 Code opened", string.Empty);

    public static ProcessExecutionResult PipeFailure(int exitCode = 1) => new(exitCode, string.Empty, PipeStderr);

    public static ProcessExecutionResult NonPipeFailure(int exitCode = 2, string standardError = NonPipeStderr) =>
        new(exitCode, string.Empty, standardError);

    /// <summary>
    /// Queues the results returned by successive <c>RunAsync</c> calls; the last result repeats once exhausted.
    /// </summary>
    public void SetupRunResults(params ProcessExecutionResult[] results) =>
        SetupRunSteps(results.Select(result => (Func<ProcessExecutionResult>)(() => result)).ToArray());

    /// <summary>
    /// Queues the behaviours of successive <c>RunAsync</c> calls (a step may throw); the last step repeats.
    /// </summary>
    public void SetupRunSteps(params Func<ProcessExecutionResult>[] steps)
    {
        _runSteps.Clear();
        _lastRunStep = null;
        foreach (var step in steps)
        {
            _runSteps.Enqueue(step);
        }
    }

    public T3AppLauncher CreateSut(T3AppLauncherOptions? options = null) =>
        new(ProcessRunner, DetachedProcessLauncher, FileSystem, Locator, DelayProvider, options);

    public T3AppLauncher CreateSutWithoutOptions() =>
        new(ProcessRunner, DetachedProcessLauncher, FileSystem, Locator, DelayProvider);
}
