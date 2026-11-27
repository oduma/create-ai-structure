namespace CreateAiStructure.Infrastructure.Provisioning;

/// <summary>
/// Parses a Git remote URL (URL form or SCP form) into an <c>owner/repo</c> slug. Host-agnostic, so it also
/// works for GitHub Enterprise hosts.
/// </summary>
internal static class RemoteOriginSlugParser
{
    private const string GitSuffix = ".git";

    /// <summary>Attempts to extract the <c>owner/repo</c> slug from a remote URL.</summary>
    /// <param name="remoteUrl">The raw remote URL, for example the output of <c>git remote get-url origin</c>.</param>
    /// <param name="slug">The <c>owner/repo</c> slug on success; otherwise <see cref="string.Empty"/>.</param>
    /// <returns><see langword="true"/> if a slug was extracted; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? remoteUrl, out string slug)
    {
        slug = string.Empty;

        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        var path = ExtractPath(remoteUrl.Trim());
        if (path is null)
        {
            return false;
        }

        path = path.Trim('/');
        if (path.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^GitSuffix.Length];
        }

        path = path.Trim('/');

        // Leading/trailing slashes are trimmed, so an empty segment always yields more than two segments.
        var segments = path.Split('/');
        if (segments.Length != 2 || path.Any(char.IsWhiteSpace))
        {
            return false;
        }

        slug = $"{segments[0]}/{segments[1]}";
        return true;
    }

    private static string? ExtractPath(string remote)
    {
        if (remote.Contains("://", StringComparison.Ordinal))
        {
            return Uri.TryCreate(remote, UriKind.Absolute, out var uri) ? uri.AbsolutePath : null;
        }

        var colonIndex = remote.IndexOf(':', StringComparison.Ordinal);
        return colonIndex > 0 ? remote[(colonIndex + 1)..] : null;
    }
}
