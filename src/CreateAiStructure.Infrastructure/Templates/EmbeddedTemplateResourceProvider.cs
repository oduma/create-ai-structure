using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Infrastructure.Templates;

/// <summary>
/// Reads templates exclusively from the <c>templates/</c> manifest resources of an assembly (Epic #2 FR-2).
/// </summary>
/// <remarks>
/// Resources are indexed once at construction: manifest names are normalised to forward slashes and only names
/// starting with <c>templates/</c> (ordinal) followed by a non-empty remainder are kept, keyed by that remainder.
/// Lookups are ordinal and exact, so wrong casing, backslashes, leading slashes, a <c>templates/</c> prefix or
/// <c>..</c> segments simply resolve to <c>TemplateResource.NotFound</c>.
/// </remarks>
public sealed class EmbeddedTemplateResourceProvider : ITemplateResourceProvider
{
    private const string ResourcePrefix = "templates/";

    private readonly Assembly _resourceAssembly;
    private readonly Dictionary<string, string> _manifestNamesByPath = new(StringComparer.Ordinal);
    private readonly ReadOnlyCollection<string> _availableTemplates;

    /// <summary>Creates a provider over the Infrastructure assembly's embedded templates.</summary>
    public EmbeddedTemplateResourceProvider()
        : this(typeof(EmbeddedTemplateResourceProvider).Assembly)
    {
    }

    /// <summary>Creates a provider over the embedded templates of <paramref name="resourceAssembly"/>.</summary>
    /// <param name="resourceAssembly">The assembly holding the <c>templates/</c> manifest resources.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceAssembly"/> is null.</exception>
    public EmbeddedTemplateResourceProvider(Assembly resourceAssembly)
    {
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        _resourceAssembly = resourceAssembly;

        foreach (var manifestName in resourceAssembly.GetManifestResourceNames())
        {
            var normalised = manifestName.Replace('\\', '/');
            if (normalised.StartsWith(ResourcePrefix, StringComparison.Ordinal) && normalised.Length > ResourcePrefix.Length)
            {
                _manifestNamesByPath.TryAdd(normalised[ResourcePrefix.Length..], manifestName);
            }
        }

        var sorted = _manifestNamesByPath.Keys.ToArray();
        Array.Sort(sorted, CompareTemplatePaths);
        _availableTemplates = Array.AsReadOnly(sorted);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Failures: <c>TemplateResource.InvalidPath</c> for a blank path, <c>TemplateResource.NotFound</c> for a path
    /// that is not an indexed template, and <c>TemplateResource.Unreadable</c> when the resource stream is missing.
    /// Content is decoded as UTF-8 with any byte-order mark removed; line endings are returned unchanged.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<Result<string>> ReadTemplateAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Result<string>.Failure(new DomainError(
                "TemplateResource.InvalidPath", "A template path must not be null, empty or whitespace."));
        }

        if (!_manifestNamesByPath.TryGetValue(relativePath, out var manifestName))
        {
            return Result<string>.Failure(new DomainError(
                "TemplateResource.NotFound", $"Template '{relativePath}' was not found."));
        }

        await using var stream = _resourceAssembly.GetManifestResourceStream(manifestName);
        if (stream is null)
        {
            return Result<string>.Failure(new DomainError(
                "TemplateResource.Unreadable", $"Template '{relativePath}' could not be opened."));
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Result<string>.Success(await reader.ReadToEndAsync(cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Paths are sorted <see cref="StringComparer.OrdinalIgnoreCase"/> then <see cref="StringComparer.Ordinal"/>,
    /// computed once, and returned as a read-only wrapper that rejects mutation through collection casts.
    /// </remarks>
    public IReadOnlyCollection<string> ListAvailableTemplates() => _availableTemplates;

    private static int CompareTemplatePaths(string left, string right)
    {
        var ignoreCase = StringComparer.OrdinalIgnoreCase.Compare(left, right);
        return ignoreCase != 0 ? ignoreCase : StringComparer.Ordinal.Compare(left, right);
    }
}
