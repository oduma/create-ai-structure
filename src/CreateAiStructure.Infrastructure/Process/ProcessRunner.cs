using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// Safely runs external commands: never through a shell, arguments passed verbatim via
/// <see cref="ProcessStartInfo.ArgumentList"/>, UTF-8 output capture without pipe deadlocks,
/// and cancellation that kills the whole process tree.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private const int FailureExitCode = -1;

    private readonly IExecutableResolver _resolver;
    private readonly IProcessStarter _starter;

    /// <summary>Initializes a new instance that resolves executables on <c>PATH</c> and starts real processes.</summary>
    /// <param name="environment">The environment variable provider used for <c>PATH</c> resolution.</param>
    /// <param name="fileSystem">The file system abstraction used for <c>PATH</c> resolution.</param>
    public ProcessRunner(IEnvironmentProvider environment, IFileSystem fileSystem)
        : this(new ExecutableResolver(environment, fileSystem), new SystemProcessStarter())
    {
    }

    /// <summary>Initializes a new instance with explicit resolution and process-start seams.</summary>
    /// <param name="resolver">Resolves bare executable names to absolute paths.</param>
    /// <param name="starter">Starts the operating-system process.</param>
    internal ProcessRunner(IExecutableResolver resolver, IProcessStarter starter)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(starter);

        _resolver = resolver;
        _starter = starter;
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

        try
        {
            await handle.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!handle.HasExited)
            {
                handle.KillEntireProcessTree();
            }

            throw;
        }

        var output = await standardOutput.ConfigureAwait(false);
        var error = await standardError.ConfigureAwait(false);
        return new ProcessExecutionResult(handle.ExitCode, output, error);
    }

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
