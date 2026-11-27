using System.ComponentModel;

namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// An <see cref="IProcessHandle"/> over a started <see cref="System.Diagnostics.Process"/> with redirected output.
/// </summary>
/// <param name="process">The process to wrap; ownership transfers to this handle.</param>
/// <param name="kill">
/// Kills the process (the argument requests the entire tree); a seam so the best-effort catch list is unit-testable.
/// </param>
internal sealed class SystemProcessHandle(System.Diagnostics.Process process, Action<bool> kill) : IProcessHandle
{
    private readonly System.Diagnostics.Process _process = process;
    private readonly Action<bool> _kill = kill;

    /// <summary>Initializes a new instance that kills via <see cref="System.Diagnostics.Process.Kill(bool)"/>.</summary>
    /// <param name="process">The process to wrap; ownership transfers to this handle.</param>
    public SystemProcessHandle(System.Diagnostics.Process process)
        : this(process, process.Kill)
    {
    }

    /// <inheritdoc />
    public bool HasExited => _process.HasExited;

    /// <inheritdoc />
    public int ExitCode => _process.ExitCode;

    /// <inheritdoc />
    public Task<string> ReadStandardOutputToEndAsync(CancellationToken cancellationToken) =>
        _process.StandardOutput.ReadToEndAsync(cancellationToken);

    /// <inheritdoc />
    public Task<string> ReadStandardErrorToEndAsync(CancellationToken cancellationToken) =>
        _process.StandardError.ReadToEndAsync(cancellationToken);

    /// <inheritdoc />
    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Best effort: an already-exited process (<see cref="InvalidOperationException"/>), a process or descendant that
    /// cannot be terminated (<see cref="Win32Exception"/>) and aggregated descendant failures
    /// (<see cref="AggregateException"/>) are ignored so they never mask the caller's cancellation.
    /// </remarks>
    public void KillEntireProcessTree()
    {
        try
        {
            _kill(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or AggregateException)
        {
            // Nothing more can be done; the caller is already unwinding (typically with cancellation).
        }
    }

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();
}
