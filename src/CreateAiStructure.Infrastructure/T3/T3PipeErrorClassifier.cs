using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Classifies failed <c>t3 app</c> invocations, deciding whether the failure indicates that the
/// T3 Code desktop app is not running or its IPC named pipe is unreachable (a cold-start
/// recoverable condition) rather than any other failure. The classifier is pure and side-effect free.
/// </summary>
public static class T3PipeErrorClassifier
{
    /// <summary>
    /// Case-insensitive text fragments that identify an unreachable T3 Code named pipe or a
    /// desktop app that is not running.
    /// </summary>
    private static readonly string[] PipeUnreachableIndicators =
    [
        @"\\.\pipe\",
        "named pipe",
        "pipe unreachable",
        "pipe is unreachable",
        "connect enoent",
        "econnrefused",
        "not running",
        "isn't running",
        "failed to connect",
        "could not connect",
        "cannot connect",
        "unable to connect",
        "connection refused",
    ];

    /// <summary>
    /// Determines whether the specified process result represents a failure caused by the T3 Code
    /// desktop app not running or its named pipe being unreachable.
    /// </summary>
    /// <param name="result">The result of a <c>t3</c> process execution to classify.</param>
    /// <returns>
    /// <see langword="true"/> when the result failed and its standard error or standard output contains
    /// a known pipe-unreachable indicator (compared case-insensitively); otherwise <see langword="false"/>,
    /// including when the result succeeded or both output streams are null, empty or whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is <see langword="null"/>.</exception>
    public static bool IsPipeUnreachable(ProcessExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Succeeded)
        {
            return false;
        }

        var standardError = result.StandardError ?? string.Empty;
        var standardOutput = result.StandardOutput ?? string.Empty;
        if (string.IsNullOrWhiteSpace(standardError) && string.IsNullOrWhiteSpace(standardOutput))
        {
            return false;
        }

        var combinedOutput = standardError + "\n" + standardOutput;
        return Array.Exists(
            PipeUnreachableIndicators,
            indicator => combinedOutput.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }
}
