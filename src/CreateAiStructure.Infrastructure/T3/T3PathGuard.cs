using System.Diagnostics.CodeAnalysis;

namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Validates paths before they are probed or executed. Only local, drive-rooted paths are accepted:
/// the guard rejects relative paths (current-directory hijacking), <c>..</c> segments (directory
/// traversal), network shares (UNC paths, which can leak NTLM credentials when probed), Win32 device
/// and extended-length paths (<c>\\?\</c>, <c>\\.\</c>, which bypass normalization), alternate data
/// streams (<c>file.exe:stream</c>), and embedded quotes or NUL characters (argument injection and
/// string truncation). The guard is pure and side-effect free.
/// </summary>
internal static class T3PathGuard
{
    /// <summary>The characters that separate path segments.</summary>
    private static readonly char[] SegmentSeparators = ['\\', '/'];

    /// <summary>Characters that must never appear in a path (quote injection, NUL truncation).</summary>
    private static readonly char[] ForbiddenCharacters = ['"', '\0'];

    /// <summary>The path segment that navigates to a parent directory.</summary>
    private const string ParentSegment = "..";

    /// <summary>The prefix shared by UNC and device paths once separators are normalized.</summary>
    private const string NetworkOrDevicePrefix = @"\\";

    /// <summary>The only index at which a volume separator (<c>:</c>) is permitted, as in <c>C:\</c>.</summary>
    private const int DriveSeparatorIndex = 1;

    /// <summary>
    /// Determines whether the specified path is a safe, fully qualified, local absolute path.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="path"/> is not <see langword="null"/>, empty or whitespace;
    /// is fully qualified; does not start with two separators (rejecting UNC shares such as
    /// <c>\\server\share</c> or <c>//server/share</c> and device paths such as <c>\\?\C:\</c> or <c>\\.\C:\</c>);
    /// contains no <c>"</c> and no NUL character; contains no <c>:</c> other than the drive separator at
    /// index 1 (rejecting alternate data streams); and contains no <c>..</c> segment (splitting on <c>\</c>
    /// and <c>/</c>). Otherwise <see langword="false"/>.
    /// </returns>
    internal static bool IsSafeAbsolutePath([NotNullWhen(true)] string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path)
        && !path.Replace('/', '\\').StartsWith(NetworkOrDevicePrefix, StringComparison.Ordinal)
        && path.IndexOfAny(ForbiddenCharacters) < 0
        && path.IndexOf(':', DriveSeparatorIndex + 1) < 0
        && !path.Split(SegmentSeparators).Contains(ParentSegment, StringComparer.Ordinal);
}
