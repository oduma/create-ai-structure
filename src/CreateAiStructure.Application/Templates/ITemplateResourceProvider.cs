using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Application.Templates;

/// <summary>
/// Reads the raw template files that are scaffolded into target repositories.
/// </summary>
public interface ITemplateResourceProvider
{
    /// <summary>Reads the content of a template.</summary>
    /// <param name="relativePath">The forward-slash relative path of the template.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>A successful result with the template content, or a failure if the template cannot be read.</returns>
    Task<Result<string>> ReadTemplateAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Lists the relative paths of all available templates.</summary>
    /// <returns>The available template paths.</returns>
    IReadOnlyCollection<string> ListAvailableTemplates();
}
