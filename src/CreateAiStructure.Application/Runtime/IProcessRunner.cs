namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// Runs an external process to completion and captures its output.
/// </summary>
public interface IProcessRunner
{
    /// <summary>Runs the requested process and waits for it to exit.</summary>
    /// <param name="request">The process to run.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>The exit code and captured standard output and error.</returns>
    Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default);
}
