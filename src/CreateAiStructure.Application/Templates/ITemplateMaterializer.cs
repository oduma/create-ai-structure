using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Templates;

namespace CreateAiStructure.Application.Templates;

/// <summary>
/// Renders the templates of a manifest and writes them into a target directory.
/// </summary>
public interface ITemplateMaterializer
{
    /// <summary>Renders and writes every file in the manifest.</summary>
    /// <param name="manifest">The files to materialize.</param>
    /// <param name="context">The values used to replace the template tokens.</param>
    /// <param name="targetDirectory">The directory the files are written into.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A successful result with the paths of the written files, or a failure describing why.</returns>
    Task<Result<IReadOnlyList<string>>> MaterializeAsync(
        TemplateManifest manifest,
        TemplateRenderContext context,
        string targetDirectory,
        CancellationToken cancellationToken = default);
}
