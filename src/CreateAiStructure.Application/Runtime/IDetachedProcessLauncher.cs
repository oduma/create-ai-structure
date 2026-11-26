using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// Starts an external process without waiting for it, for example to cold-start the T3 Code desktop app.
/// </summary>
public interface IDetachedProcessLauncher
{
    /// <summary>Starts the requested process detached from the current one.</summary>
    /// <param name="request">The process to start.</param>
    /// <returns>A successful result if the process started; otherwise a failure describing why.</returns>
    Result StartDetached(ProcessExecutionRequest request);
}
