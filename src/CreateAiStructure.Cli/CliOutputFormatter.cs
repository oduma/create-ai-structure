using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Cli;

/// <summary>Builds the user-facing text lines written by <see cref="CliRunner"/>.</summary>
public static class CliOutputFormatter
{
    /// <summary>The message written to stderr when the operation is cancelled.</summary>
    public const string CancelledMessage = "Operation cancelled.";

    /// <summary>The T3 status line when the desktop app was started by this run.</summary>
    public const string T3StartedMessage = "Started the T3 Code desktop app.";

    /// <summary>The T3 status line when the desktop app was already running.</summary>
    public const string T3AlreadyRunningMessage = "T3 Code desktop app was already running.";

    /// <summary>Formats the version line: <c>create-ai-structure &lt;version&gt;</c>.</summary>
    /// <param name="version">The version text.</param>
    /// <returns>The version line.</returns>
    public static string FormatVersion(string version) => $"create-ai-structure {version}";

    /// <summary>Formats an error line: <c>Error [&lt;Code&gt;]: &lt;Message&gt;</c>.</summary>
    /// <param name="error">The error to format.</param>
    /// <returns>The error line.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    public static string FormatError(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return $"Error [{error.Code}]: {error.Message}";
    }

    /// <summary>Formats the success summary lines for a completed scaffold run.</summary>
    /// <param name="result">The scaffold outcome.</param>
    /// <returns>The repository, file count, T3 status and import reminder lines, in that order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static IReadOnlyList<string> FormatSuccess(ScaffoldResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return
        [
            $"Repository created at {result.RepositoryDirectory}",
            $"Materialized {result.MaterializedFiles.Count} files.",
            result.StartedT3DesktopApp ? T3StartedMessage : T3AlreadyRunningMessage,
            result.T3ImportReminder,
        ];
    }
}
