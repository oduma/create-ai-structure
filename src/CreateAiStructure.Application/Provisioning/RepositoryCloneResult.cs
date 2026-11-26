namespace CreateAiStructure.Application.Provisioning;

/// <summary>
/// The local directory and GitHub slug of a newly created and cloned repository.
/// </summary>
/// <param name="RepositoryDirectory">The local clone directory; must not be null, empty or whitespace.</param>
/// <param name="RepositorySlug">The GitHub <c>owner/name</c> slug; must not be null, empty or whitespace.</param>
/// <remarks>
/// Validation runs only in the constructor. A <see langword="with"/> expression bypasses it; this is accepted (KISS).
/// </remarks>
public sealed record RepositoryCloneResult(string RepositoryDirectory, string RepositorySlug)
{
    /// <summary>Gets the local clone directory.</summary>
    public string RepositoryDirectory { get; init; } = RequireText(RepositoryDirectory, nameof(RepositoryDirectory));

    /// <summary>Gets the GitHub <c>owner/name</c> slug.</summary>
    public string RepositorySlug { get; init; } = RequireText(RepositorySlug, nameof(RepositorySlug));

    private static string RequireText(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }
}
