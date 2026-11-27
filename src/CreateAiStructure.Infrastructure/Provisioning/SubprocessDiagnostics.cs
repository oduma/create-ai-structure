using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Provisioning;

/// <summary>
/// Formats the FR-6 diagnostic message for a failed external process step.
/// </summary>
internal static class SubprocessDiagnostics
{
    private const string NoOutputDetail = "No diagnostic output was captured.";

    /// <summary>
    /// Describes a failed step as <c>"{step} failed with exit code {ExitCode}: {detail}"</c>, where the detail is the
    /// trimmed standard error, else the trimmed standard output, else a fixed fallback text.
    /// </summary>
    /// <param name="step">The human-readable step name, for example <c>gh repo create</c>.</param>
    /// <param name="result">The captured process outcome.</param>
    /// <returns>The formatted diagnostic message.</returns>
    public static string Describe(string step, ProcessExecutionResult result)
    {
        var detail = FirstNonBlank(result.StandardError) ?? FirstNonBlank(result.StandardOutput) ?? NoOutputDetail;
        return $"{step} failed with exit code {result.ExitCode}: {detail}";
    }

    private static string? FirstNonBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
