namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// Describes an external process to execute: the executable, its arguments and an optional working directory.
/// </summary>
/// <param name="FileName">The executable name or path; must not be null, empty or whitespace.</param>
/// <param name="Arguments">The arguments passed to the executable; must not be null.</param>
/// <param name="WorkingDirectory">The working directory, or <see langword="null"/> to use the current directory.</param>
/// <remarks>
/// Validation runs only in the constructor. A <see langword="with"/> expression bypasses it; this is accepted (KISS).
/// </remarks>
public sealed record ProcessExecutionRequest(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory = null)
{
    /// <summary>Gets the executable name or path.</summary>
    public string FileName { get; init; } = RequireText(FileName, nameof(FileName));

    /// <summary>Gets the arguments passed to the executable.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = RequireNotNull(Arguments, nameof(Arguments));

    private static string RequireText(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    private static IReadOnlyList<string> RequireNotNull(IReadOnlyList<string> value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }
}
