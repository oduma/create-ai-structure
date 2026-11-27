using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Infrastructure.Templates;

/// <summary>
/// Path-containment security guard for template materialization (Epic #2 FR-4.2).
/// Rejects null/blank, backslash, NUL, drive-qualified, rooted and <c>..</c> traversal paths, and verifies that the
/// resolved destination lies strictly beneath the target root before any filesystem access is performed.
/// </summary>
/// <remarks>
/// This guard operates purely on path strings. Symlink and reparse-point enforcement is owned by the downstream
/// <c>IFileSystem</c> implementation.
/// </remarks>
internal static class TemplatePathGuard
{
    private const string UnsafeTemplatePathCode = "TemplateMaterializer.UnsafeTemplatePath";

    /// <summary>
    /// Resolves <paramref name="relativePath"/> against <paramref name="normalizedRoot"/>, rejecting unsafe paths.
    /// Unsafe inputs are rejected as a failure result before normalization, so <see cref="Path.GetFullPath(string)"/>
    /// is only ever called on pre-validated input and is not expected to throw.
    /// </summary>
    /// <remarks>
    /// No exception is caught here: an unexpected platform exception from <see cref="Path.GetFullPath(string)"/>
    /// (for example <see cref="PathTooLongException"/>) propagates to the caller.
    /// </remarks>
    /// <param name="normalizedRoot">The target root, already normalized via <see cref="Path.GetFullPath(string)"/>.</param>
    /// <param name="relativePath">The forward-slash relative template path.</param>
    /// <returns>The full destination path, or a <c>TemplateMaterializer.UnsafeTemplatePath</c> failure.</returns>
    public static Result<string> ResolveDestinationPath(string normalizedRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.Contains('\\')
            || relativePath.Contains('\0')
            || relativePath.Contains(':')
            || Path.IsPathRooted(relativePath)
            || relativePath.Split('/').Contains(".."))
        {
            return Unsafe(relativePath);
        }

        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

        return IsContained(normalizedRoot, fullPath)
            ? Result<string>.Success(fullPath)
            : Unsafe(relativePath);
    }

    /// <summary>
    /// Determines whether <paramref name="fullPath"/> lies strictly beneath <paramref name="normalizedRoot"/>.
    /// Uses ordinal comparison against the root plus a trailing directory separator, which blocks sibling-prefix
    /// escapes such as <c>repo-evil</c> and treats the root itself as not contained.
    /// </summary>
    /// <param name="normalizedRoot">The normalized root directory.</param>
    /// <param name="fullPath">The normalized candidate path.</param>
    /// <returns><see langword="true"/> when the path is contained beneath the root; otherwise <see langword="false"/>.</returns>
    public static bool IsContained(string normalizedRoot, string fullPath)
    {
        var rootPrefix = Path.EndsInDirectorySeparator(normalizedRoot)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(rootPrefix, StringComparison.Ordinal) && fullPath.Length > rootPrefix.Length;
    }

    private static Result<string> Unsafe(string? relativePath) =>
        Result<string>.Failure(new DomainError(
            UnsafeTemplatePathCode,
            $"Template path '{relativePath}' is unsafe: it must be a forward-slash relative path that stays beneath the target directory."));
}
