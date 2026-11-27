using System.Diagnostics.CodeAnalysis;

namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Validates paths before they are probed or executed, guarding against current-directory
/// hijacking (relative paths) and directory traversal (<c>..</c> segments). The guard is pure and
/// side-effect free.
/// </summary>
internal static class T3PathGuard
{
    /// <summary>The characters that separate path segments.</summary>
    private static readonly char[] SegmentSeparators = ['\\', '/'];

    /// <summary>The path segment that navigates to a parent directory.</summary>
    private const string ParentSegment = "..";

    /// <summary>
    /// Determines whether the specified path is a safe, fully qualified absolute path.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="path"/> is not <see langword="null"/>, empty or whitespace,
    /// is fully qualified, and contains no <c>..</c> segment (splitting on <c>\</c> and <c>/</c>);
    /// otherwise <see langword="false"/>.
    /// </returns>
    internal static bool IsSafeAbsolutePath([NotNullWhen(true)] string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path)
        && !path.Split(SegmentSeparators).Contains(ParentSegment, StringComparer.Ordinal);
}
