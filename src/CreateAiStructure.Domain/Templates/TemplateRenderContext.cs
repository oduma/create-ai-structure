using CreateAiStructure.Domain.Scaffolding;

namespace CreateAiStructure.Domain.Templates;

/// <summary>The immutable set of values used to render template placeholders.</summary>
public sealed class TemplateRenderContext
{
    /// <summary>Creates a render context.</summary>
    /// <param name="repositoryName">The target repository name.</param>
    /// <param name="manifest">The template manifest for the target project type.</param>
    /// <param name="repositorySlug">
    /// The repository slug (for example <c>owner/repo</c>); when <see langword="null"/> or whitespace,
    /// <see cref="Scaffolding.RepositoryName.Value"/> is used instead.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="repositoryName"/> or <paramref name="manifest"/> is <see langword="null"/>.
    /// </exception>
    public TemplateRenderContext(RepositoryName repositoryName, TemplateManifest manifest, string? repositorySlug = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryName);
        ArgumentNullException.ThrowIfNull(manifest);

        RepositoryName = repositoryName;
        RepositorySlug = string.IsNullOrWhiteSpace(repositorySlug) ? repositoryName.Value : repositorySlug;
        ConstitutionFiles = manifest.ConstitutionFiles;
        AgentFiles = manifest.AgentFiles;
    }

    /// <summary>Gets the target repository name.</summary>
    public RepositoryName RepositoryName { get; }

    /// <summary>Gets the repository slug.</summary>
    public string RepositorySlug { get; }

    /// <summary>Gets the constitution files from the manifest.</summary>
    public IReadOnlyList<string> ConstitutionFiles { get; }

    /// <summary>Gets the agent files from the manifest.</summary>
    public IReadOnlyList<string> AgentFiles { get; }
}
