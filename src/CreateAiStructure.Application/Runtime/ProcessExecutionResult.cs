namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// The captured outcome of an executed external process.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The captured standard output.</param>
/// <param name="StandardError">The captured standard error.</param>
public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Gets a value indicating whether the process exited with code zero.</summary>
    public bool Succeeded => ExitCode == 0;
}
