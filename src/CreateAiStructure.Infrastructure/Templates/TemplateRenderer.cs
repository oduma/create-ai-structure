using System.Text.RegularExpressions;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Templates;

namespace CreateAiStructure.Infrastructure.Templates;

/// <summary>
/// Replaces the <see cref="TemplateTokens"/> in template content in a single, non-recursive pass (Epic #2 FR-3).
/// </summary>
/// <remarks>
/// Matching is ordinal and case-sensitive. Replacement values are never scanned again, so a value that itself
/// looks like a token is emitted literally. Unknown placeholders and stray braces are preserved verbatim.
/// </remarks>
public sealed class TemplateRenderer : ITemplateRenderer
{
    private const string CrLf = "\r\n";
    private const string Lf = "\n";

    /// <summary>
    /// Matches any one of the five known tokens; built from the <see cref="TemplateTokens"/> constants.
    /// </summary>
    private static readonly Regex TokenPattern = new(
        string.Join(
            "|",
            Regex.Escape(TemplateTokens.RepoNameLegacy),
            Regex.Escape(TemplateTokens.RepoName),
            Regex.Escape(TemplateTokens.RepoSlug),
            Regex.Escape(TemplateTokens.ConstitutionFilesList),
            Regex.Escape(TemplateTokens.AgentsList)),
        RegexOptions.CultureInvariant);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="templateContent"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public string Render(string templateContent, TemplateRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(templateContent);
        ArgumentNullException.ThrowIfNull(context);

        var newline = templateContent.Contains(CrLf, StringComparison.Ordinal) ? CrLf : Lf;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TemplateTokens.RepoNameLegacy] = context.RepositoryName.Value,
            [TemplateTokens.RepoName] = context.RepositoryName.Value,
            [TemplateTokens.RepoSlug] = context.RepositorySlug,
            [TemplateTokens.ConstitutionFilesList] = string.Join(newline, context.ConstitutionFiles),
            [TemplateTokens.AgentsList] = string.Join(newline, context.AgentFiles),
        };

        return TokenPattern.Replace(templateContent, match => values[match.Value]);
    }
}
