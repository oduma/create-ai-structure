using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// Safely runs external commands: never through a shell, arguments passed verbatim via
/// <see cref="ProcessStartInfo.ArgumentList"/>, UTF-8 output capture without pipe deadlocks,
/// a bounded output drain after exit, refusal of cmd.exe metacharacters for batch-file targets,
/// and cancellation that kills the whole process tree.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private const int FailureExitCode = -1;

    private const string OutputDrainTimedOutMessage =
        "Output capture timed out after the process exited; a child process may still hold the output stream.";

    private static readonly TimeSpan DefaultOutputDrainTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Characters cmd.exe interprets when it re-parses a <c>.cmd</c>/<c>.bat</c> command line, which the
    /// <see cref="ProcessStartInfo.ArgumentList"/> quoting cannot neutralise.
    /// </summary>
    private static readonly SearchValues<char> UnsafeBatchArgumentCharacters = SearchValues.Create("\"%!^&|<>()\r\n");

    private readonly IExecutableResolver _resolver;
    private readonly IProcessStarter _starter;
    private readonly TimeSpan _outputDrainTimeout;

    /// <summary>Initializes a new instance that resolves executables on <c>PATH</c> and starts real processes.</summary>
    /// <param name="environment">The environment variable provider used for <c>PATH</c> resolution.</param>
    /// <param name="fileSystem">The file system abstraction used for <c>PATH</c> resolution.</param>
    public ProcessRunner(IEnvironmentProvider environment, IFileSystem fileSystem)
        : this(new ExecutableResolver(environment, fileSystem), new SystemProcessStarter())
    {
    }

    /// <summary>
    /// Initializes a new instance with explicit resolution and process-start seams and a 10 second output drain timeout.
    /// </summary>
    /// <param name="resolver">Resolves bare executable names to absolute paths.</param>
    /// <param name="starter">Starts the operating-system process.</param>
    internal ProcessRunner(IExecutableResolver resolver, IProcessStarter starter)
        : this(resolver, starter, DefaultOutputDrainTimeout)
    {
    }

    /// <summary>Initializes a new instance with explicit resolution and process-start seams.</summary>
    /// <param name="resolver">Resolves bare executable names to absolute paths.</param>
    /// <param name="starter">Starts the operating-system process.</param>
    /// <param name="outputDrainTimeout">
    /// How long to keep reading output after the process exits; a grandchild holding the inherited pipes could
    /// otherwise keep the reads open indefinitely.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> or <paramref name="starter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outputDrainTimeout"/> is not positive.</exception>
    internal ProcessRunner(IExecutableResolver resolver, IProcessStarter starter, TimeSpan outputDrainTimeout)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(outputDrainTimeout, TimeSpan.Zero);

        _resolver = resolver;
        _starter = starter;
        _outputDrainTimeout = outputDrainTimeout;
    }

    /// <inheritdoc />
    public async Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string executable;
        if (ExecutableResolver.IsBareName(request.FileName))
        {
            var resolved = _resolver.Resolve(request.FileName);
            if (resolved is null)
            {
                return Failure($"Executable '{request.FileName}' was not found on PATH.");
            }

            executable = resolved;
        }
        else if (Path.IsPathFullyQualified(request.FileName))
        {
            executable = request.FileName;
        }
        else
        {
            return Failure(
                $"Relative executable path '{request.FileName}' is not permitted; specify a command name on PATH or a fully qualified path.");
        }

        var unsafeArgumentIndex = FindUnsafeBatchArgument(executable, request.Arguments);
        if (unsafeArgumentIndex >= 0)
        {
            return Failure(
                $"Argument {unsafeArgumentIndex} contains characters that are unsafe to pass to the batch file '{request.FileName}'.");
        }

        IProcessHandle? started;
        try
        {
            started = _starter.Start(CreateStartInfo(executable, request));
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return Failure($"Failed to start '{request.FileName}': {ex.Message}");
        }

        if (started is null)
        {
            return Failure($"Failed to start '{request.FileName}'.");
        }

        using var handle = started;
        var standardOutput = handle.ReadStandardOutputToEndAsync(cancellationToken);
        var standardError = handle.ReadStandardErrorToEndAsync(cancellationToken);
        var drain = Task.WhenAll(standardOutput, standardError);

        string[] captured;
        try
        {
            await handle.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            captured = await drain.WaitAsync(_outputDrainTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            ObserveFaults(drain);
            return DrainTimedOut(handle.ExitCode, standardOutput, standardError);
        }
        catch (OperationCanceledException)
        {
            ObserveFaults(drain);
            if (!handle.HasExited)
            {
                handle.KillEntireProcessTree();
            }

            throw;
        }

        return new ProcessExecutionResult(handle.ExitCode, captured[0], captured[1]);
    }

    /// <summary>
    /// Returns the index of the first argument containing a cmd.exe metacharacter when <paramref name="executable"/>
    /// is a <c>.cmd</c>/<c>.bat</c> file (which Windows always runs through cmd.exe); otherwise <c>-1</c>.
    /// </summary>
    private static int FindUnsafeBatchArgument(string executable, IReadOnlyList<string> arguments)
    {
        if (!executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            && !executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].AsSpan().ContainsAny(UnsafeBatchArgumentCharacters))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Marks an abandoned task's eventual fault as observed so it never raises <see cref="TaskScheduler.UnobservedTaskException"/>.
    /// </summary>
    private static void ObserveFaults(Task task) =>
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    /// <summary>Builds the result for an exited process whose output could not be fully drained in time.</summary>
    private static ProcessExecutionResult DrainTimedOut(int exitCode, Task<string> standardOutput, Task<string> standardError)
    {
        var error = CompletedTextOrEmpty(standardError);
        var message = error.Length == 0
            ? OutputDrainTimedOutMessage
            : $"{error}{Environment.NewLine}{OutputDrainTimedOutMessage}";
        return new ProcessExecutionResult(exitCode, CompletedTextOrEmpty(standardOutput), message);
    }

    private static string CompletedTextOrEmpty(Task<string> read) =>
        read.IsCompletedSuccessfully ? read.Result : string.Empty;

    private static ProcessStartInfo CreateStartInfo(string executable, ProcessExecutionRequest request)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        return startInfo;
    }

    private static ProcessExecutionResult Failure(string message) => new(FailureExitCode, string.Empty, message);
}
