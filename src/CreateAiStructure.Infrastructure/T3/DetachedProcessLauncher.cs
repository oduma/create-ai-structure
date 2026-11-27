using System.ComponentModel;
using System.Diagnostics;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Starts an external process (such as the T3 Code desktop app) detached from the current one: no
/// shell, no standard stream redirection, and arguments passed verbatim through
/// <see cref="ProcessStartInfo.ArgumentList"/>. Known OS start failures are turned into
/// <see cref="Result"/> failures; the launcher never waits for the started process.
/// </summary>
public sealed class DetachedProcessLauncher : IDetachedProcessLauncher
{
    /// <summary>The error code returned when the executable path is not a safe absolute path.</summary>
    private const string InvalidExecutablePathErrorCode = "T3Launch.InvalidExecutablePath";

    /// <summary>The error code returned when the operating system fails to start the process.</summary>
    private const string DesktopStartFailedErrorCode = "T3Launch.DesktopStartFailed";

    /// <summary>Starts a process from start information and returns its disposable handle, or <see langword="null"/>.</summary>
    private readonly Func<ProcessStartInfo, IDisposable?> _processStarter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetachedProcessLauncher"/> class that starts
    /// processes through <see cref="Process.Start(ProcessStartInfo)"/>.
    /// </summary>
    public DetachedProcessLauncher()
        : this(static startInfo => Process.Start(startInfo))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DetachedProcessLauncher"/> class with the
    /// specified process starter.
    /// </summary>
    /// <param name="processStarter">
    /// Starts a process from the supplied start information and returns a handle to it, or
    /// <see langword="null"/> when no process was started. Disposing the handle must not terminate
    /// the started process.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="processStarter"/> is <see langword="null"/>.</exception>
    public DetachedProcessLauncher(Func<ProcessStartInfo, IDisposable?> processStarter)
    {
        ArgumentNullException.ThrowIfNull(processStarter);
        _processStarter = processStarter;
    }

    /// <summary>Starts the requested process detached from the current one.</summary>
    /// <param name="request">The process to start; its file name must be a safe absolute path.</param>
    /// <returns>
    /// A successful result if the process started; a <c>T3Launch.InvalidExecutablePath</c> failure
    /// when the file name is relative or contains a <c>..</c> segment (nothing is started); otherwise a
    /// <c>T3Launch.DesktopStartFailed</c> failure when the operating system did not start the process.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    public Result StartDetached(ProcessExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!T3PathGuard.IsSafeAbsolutePath(request.FileName))
        {
            return Result.Failure(new DomainError(
                InvalidExecutablePathErrorCode,
                $"The executable path '{request.FileName}' is not a safe absolute path."));
        }

        var startInfo = CreateStartInfo(request);
        try
        {
            using var process = _processStarter(startInfo);
            return process is null
                ? Result.Failure(new DomainError(
                    DesktopStartFailedErrorCode,
                    $"The process '{startInfo.FileName}' could not be started."))
                : Result.Success();
        }
        catch (Exception ex) when (ex is Win32Exception
                                       or FileNotFoundException
                                       or InvalidOperationException
                                       or UnauthorizedAccessException)
        {
            return Result.Failure(new DomainError(
                DesktopStartFailedErrorCode,
                $"The process '{startInfo.FileName}' could not be started: {ex.Message}"));
        }
    }

    /// <summary>
    /// Builds shell-free, non-redirected start information that passes each argument verbatim.
    /// </summary>
    /// <param name="request">The process to start.</param>
    /// <returns>The start information for <paramref name="request"/>.</returns>
    private static ProcessStartInfo CreateStartInfo(ProcessExecutionRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
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
}
