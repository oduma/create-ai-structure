namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// An <see cref="IProcessHandle"/> over a started <see cref="System.Diagnostics.Process"/> with redirected output.
/// </summary>
/// <param name="process">The process to wrap; ownership transfers to this handle.</param>
internal sealed class SystemProcessHandle(System.Diagnostics.Process process) : IProcessHandle
{
    private readonly System.Diagnostics.Process _process = process;

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
    public void KillEntireProcessTree()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited (or was never associated); nothing to kill.
        }
    }

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();
}
