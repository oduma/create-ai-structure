namespace CreateAiStructure.Application.Provisioning;

/// <summary>
/// Checks that the required command-line tools are installed.
/// </summary>
public interface IToolValidator
{
    /// <summary>Checks each required tool.</summary>
    /// <param name="requiredTools">The names of the tools to check.</param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The tools that were found and the tools that are missing.</returns>
    Task<ToolValidationResult> ValidateRequiredToolsAsync(
        IReadOnlyList<string> requiredTools,
        CancellationToken cancellationToken = default);
}
