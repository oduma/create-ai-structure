using CreateAiStructure.Domain.Templates;

namespace CreateAiStructure.Application.Templates;

/// <summary>
/// Replaces template tokens with values from a render context.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>Renders template content.</summary>
    /// <param name="templateContent">The raw template content.</param>
    /// <param name="context">The values used to replace the template tokens.</param>
    /// <returns>The rendered content.</returns>
    string Render(string templateContent, TemplateRenderContext context);
}
