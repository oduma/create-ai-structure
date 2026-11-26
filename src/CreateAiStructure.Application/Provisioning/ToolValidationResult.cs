namespace CreateAiStructure.Application.Provisioning;

/// <summary>
/// The outcome of checking which required command-line tools are installed.
/// </summary>
/// <param name="FoundTools">The tools that were found; must not be null.</param>
/// <param name="MissingTools">The tools that were not found; must not be null.</param>
/// <remarks>
/// Validation runs only in the constructor. A <see langword="with"/> expression bypasses it; this is accepted (KISS).
/// </remarks>
public sealed record ToolValidationResult(IReadOnlyList<string> FoundTools, IReadOnlyList<string> MissingTools)
{
    /// <summary>Gets the tools that were found.</summary>
    public IReadOnlyList<string> FoundTools { get; init; } = RequireNotNull(FoundTools, nameof(FoundTools));

    /// <summary>Gets the tools that were not found.</summary>
    public IReadOnlyList<string> MissingTools { get; init; } = RequireNotNull(MissingTools, nameof(MissingTools));

    /// <summary>Gets a value indicating whether no required tool is missing.</summary>
    public bool IsValid => MissingTools.Count == 0;

    private static IReadOnlyList<string> RequireNotNull(IReadOnlyList<string> value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }
}
