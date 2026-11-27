using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Launches the T3 Code desktop app for a repository through the <c>t3 app</c> CLI command. When the
/// app is not running (its named pipe is unreachable) the launcher performs a cold start: it locates
/// the desktop executable, starts it detached and retries <c>t3 app</c> a bounded number of times.
/// Any other <c>t3 app</c> failure fails fast. Cancellation always propagates as an
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class T3AppLauncher : IT3AppLauncher
{
    /// <summary>The reminder shown after a successful launch, telling the user to import <c>t3.json</c>.</summary>
    public const string ImportReminderMessage = "Reminder: Import t3.json in T3 Code to enable workspace scripts.";

    /// <summary>The error code returned when the repository directory does not exist.</summary>
    private const string RepositoryDirectoryNotFoundErrorCode = "T3Launch.RepositoryDirectoryNotFound";

    /// <summary>The error code returned when <c>t3 app</c> fails for a reason other than an unreachable pipe.</summary>
    private const string CommandFailedErrorCode = "T3Launch.CommandFailed";

    /// <summary>The error code returned when the started desktop app never became reachable.</summary>
    private const string ReadinessTimeoutErrorCode = "T3Launch.ReadinessTimeout";

    /// <summary>The diagnostics used when a failed process wrote nothing to either output stream.</summary>
    private const string NoDiagnosticsMessage = "No diagnostic output was captured.";

    /// <summary>The T3 CLI executable name.</summary>
    private const string T3FileName = "t3";

    /// <summary>The arguments that open the T3 Code app for the working directory.</summary>
    private static readonly string[] T3AppArguments = ["app"];

    private readonly IProcessRunner _processRunner;
    private readonly IDetachedProcessLauncher _detachedProcessLauncher;
    private readonly IFileSystem _fileSystem;
    private readonly IT3DesktopExecutableLocator _desktopExecutableLocator;
    private readonly IDelayProvider _delayProvider;
    private readonly T3AppLauncherOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="T3AppLauncher"/> class.
    /// </summary>
    /// <param name="processRunner">Runs the <c>t3 app</c> command and captures its output.</param>
    /// <param name="detachedProcessLauncher">Starts the T3 Code desktop app detached on a cold start.</param>
    /// <param name="fileSystem">Checks that the repository directory exists.</param>
    /// <param name="desktopExecutableLocator">Locates the T3 Code desktop executable on a cold start.</param>
    /// <param name="delayProvider">Waits between readiness probes.</param>
    /// <param name="options">The readiness timing and retry settings, or <see langword="null"/> for <see cref="T3AppLauncherOptions.Default"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
    public T3AppLauncher(
        IProcessRunner processRunner,
        IDetachedProcessLauncher detachedProcessLauncher,
        IFileSystem fileSystem,
        IT3DesktopExecutableLocator desktopExecutableLocator,
        IDelayProvider delayProvider,
        T3AppLauncherOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(detachedProcessLauncher);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(desktopExecutableLocator);
        ArgumentNullException.ThrowIfNull(delayProvider);

        _processRunner = processRunner;
        _detachedProcessLauncher = detachedProcessLauncher;
        _fileSystem = fileSystem;
        _desktopExecutableLocator = desktopExecutableLocator;
        _delayProvider = delayProvider;
        _options = options ?? T3AppLauncherOptions.Default;
    }

    /// <summary>Launches T3 Code for the repository, cold-starting the desktop app when it is not running.</summary>
    /// <param name="repositoryDirectory">The local repository directory; must not be null, empty or whitespace.</param>
    /// <param name="cancellationToken">A token that cancels the launch.</param>
    /// <returns>
    /// A successful result whose outcome states whether the desktop app was started and carries
    /// <see cref="ImportReminderMessage"/>; otherwise a <c>T3Launch.RepositoryDirectoryNotFound</c>,
    /// <c>T3Launch.CommandFailed</c> or <c>T3Launch.ReadinessTimeout</c> failure, or the error returned by
    /// the desktop executable locator or detached process launcher.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="repositoryDirectory"/> is null, empty or whitespace.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<Result<T3LaunchOutcome>> LaunchAsync(string repositoryDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_fileSystem.DirectoryExists(repositoryDirectory))
        {
            return Result<T3LaunchOutcome>.Failure(new DomainError(
                RepositoryDirectoryNotFoundErrorCode,
                $"The repository directory '{repositoryDirectory}' does not exist."));
        }

        var initialResult = await RunT3AppAsync(repositoryDirectory, cancellationToken);
        if (initialResult.Succeeded)
        {
            return Launched(startedDesktopApp: false);
        }

        return T3PipeErrorClassifier.IsPipeUnreachable(initialResult)
            ? await ColdStartAsync(repositoryDirectory, initialResult, cancellationToken)
            : CommandFailed(repositoryDirectory, initialResult);
    }

    /// <summary>
    /// Locates and starts the desktop app detached, then probes <c>t3 app</c> up to
    /// <see cref="T3AppLauncherOptions.MaxRetryAttempts"/> times without a trailing delay.
    /// </summary>
    /// <param name="repositoryDirectory">The repository directory to open.</param>
    /// <param name="initialResult">The pipe-unreachable result of the initial <c>t3 app</c> run.</param>
    /// <param name="cancellationToken">A token that cancels the cold start.</param>
    /// <returns>The launch result.</returns>
    private async Task<Result<T3LaunchOutcome>> ColdStartAsync(
        string repositoryDirectory,
        ProcessExecutionResult initialResult,
        CancellationToken cancellationToken)
    {
        var located = _desktopExecutableLocator.Locate();
        if (located.IsFailure)
        {
            return Result<T3LaunchOutcome>.Failure(located.Error);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var executablePath = located.Value;
        var started = _detachedProcessLauncher.StartDetached(new ProcessExecutionRequest(executablePath, []));
        if (started.IsFailure)
        {
            return Result<T3LaunchOutcome>.Failure(started.Error);
        }

        var lastResult = initialResult;
        for (var attempt = 1; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            var delay = attempt == 1 ? _options.InitialStartupDelay : _options.RetryInterval;
            await _delayProvider.DelayAsync(delay, cancellationToken);

            lastResult = await RunT3AppAsync(repositoryDirectory, cancellationToken);
            if (lastResult.Succeeded)
            {
                return Launched(startedDesktopApp: true);
            }

            if (!T3PipeErrorClassifier.IsPipeUnreachable(lastResult))
            {
                return CommandFailed(repositoryDirectory, lastResult);
            }
        }

        return ReadinessTimeout(executablePath, lastResult);
    }

    /// <summary>Runs <c>t3 app</c> in the repository directory.</summary>
    /// <param name="repositoryDirectory">The working directory for the command.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>The captured process result.</returns>
    private Task<ProcessExecutionResult> RunT3AppAsync(string repositoryDirectory, CancellationToken cancellationToken) =>
        _processRunner.RunAsync(new ProcessExecutionRequest(T3FileName, T3AppArguments, repositoryDirectory), cancellationToken);

    /// <summary>Creates the successful launch result carrying the import reminder.</summary>
    /// <param name="startedDesktopApp">Whether the desktop app was cold-started.</param>
    /// <returns>The successful launch result.</returns>
    private static Result<T3LaunchOutcome> Launched(bool startedDesktopApp) =>
        Result<T3LaunchOutcome>.Success(new T3LaunchOutcome(startedDesktopApp, ImportReminderMessage));

    /// <summary>Creates the <c>T3Launch.CommandFailed</c> failure for a non-pipe <c>t3 app</c> failure.</summary>
    /// <param name="repositoryDirectory">The directory <c>t3 app</c> ran in.</param>
    /// <param name="result">The failed process result.</param>
    /// <returns>The failure result.</returns>
    private static Result<T3LaunchOutcome> CommandFailed(string repositoryDirectory, ProcessExecutionResult result) =>
        Result<T3LaunchOutcome>.Failure(new DomainError(
            CommandFailedErrorCode,
            FormatFailure($"'t3 app' failed in '{repositoryDirectory}'", result)));

    /// <summary>Creates the <c>T3Launch.ReadinessTimeout</c> failure after every readiness probe failed.</summary>
    /// <param name="executablePath">The started desktop executable path.</param>
    /// <param name="lastResult">The result of the final probe.</param>
    /// <returns>The failure result.</returns>
    private Result<T3LaunchOutcome> ReadinessTimeout(string executablePath, ProcessExecutionResult lastResult) =>
        Result<T3LaunchOutcome>.Failure(new DomainError(
            ReadinessTimeoutErrorCode,
            FormatFailure(
                $"T3 Code was started from '{executablePath}' but 't3 app' could not reach it after {_options.MaxRetryAttempts} retry attempts; the last attempt failed",
                lastResult)
            + ". Wait for T3 Code to finish starting, then open the repository and import t3.json manually."));

    /// <summary>
    /// Formats a failure message (FR-6): the context, the exit code, then the trimmed standard error,
    /// else the trimmed standard output, else a fixed no-diagnostics message.
    /// </summary>
    /// <param name="context">The description of what failed.</param>
    /// <param name="result">The failed process result.</param>
    /// <returns>The formatted failure message.</returns>
    private static string FormatFailure(string context, ProcessExecutionResult result)
    {
        var diagnostics = !string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardError.Trim()
            : !string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardOutput.Trim()
                : NoDiagnosticsMessage;

        return $"{context} with exit code {result.ExitCode}: {diagnostics}";
    }
}
