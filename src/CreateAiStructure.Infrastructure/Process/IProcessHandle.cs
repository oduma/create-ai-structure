namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// A started external process whose redirected output can be read and whose lifetime can be awaited or ended.
/// </summary>
internal interface IProcessHandle : IDisposable
{
    /// <summary>Gets a value indicating whether the process has exited.</summary>
    bool HasExited { get; }

    /// <summary>Gets the exit code of the process; valid only after it has exited.</summary>
    int ExitCode { get; }

    /// <summary>Reads the redirected standard output stream to its end.</summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The complete standard output text.</returns>
    Task<string> ReadStandardOutputToEndAsync(CancellationToken cancellationToken);

    /// <summary>Reads the redirected standard error stream to its end.</summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The complete standard error text.</returns>
    Task<string> ReadStandardErrorToEndAsync(CancellationToken cancellationToken);

    /// <summary>Waits for the process to exit.</summary>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the process has exited.</returns>
    Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Kills the process and all of its descendants, ignoring the race where the process has already exited.
    /// </summary>
    void KillEntireProcessTree();
}
