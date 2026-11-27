using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// Deterministic, current-directory-hijack-proof executable resolver built on <c>PATH</c> and, on Windows, <c>PATHEXT</c>.
/// Only fully qualified <c>PATH</c> entries are searched.
/// </summary>
internal sealed class ExecutableResolver : IExecutableResolver
{
    private const string PathVariable = "PATH";
    private const string PathExtVariable = "PATHEXT";
    private const char PathExtSeparator = ';';
    private const char Quote = '"';

    private static readonly string[] DefaultExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    private const StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

    private readonly IEnvironmentProvider _environment;
    private readonly IFileSystem _fileSystem;
    private readonly char _pathSeparator;
    private readonly bool _probePathExtensions;

    /// <summary>Initializes a new instance using the current platform's path separator and extension conventions.</summary>
    /// <param name="environment">The environment variable provider.</param>
    /// <param name="fileSystem">The file system abstraction used to probe candidates.</param>
    public ExecutableResolver(IEnvironmentProvider environment, IFileSystem fileSystem)
        : this(environment, fileSystem, Path.PathSeparator, OperatingSystem.IsWindows())
    {
    }

    /// <summary>Initializes a new instance with an explicit path separator and extension probing mode.</summary>
    /// <param name="environment">The environment variable provider.</param>
    /// <param name="fileSystem">The file system abstraction used to probe candidates.</param>
    /// <param name="pathSeparator">The separator between <c>PATH</c> entries.</param>
    /// <param name="probePathExtensions"><see langword="true"/> to probe <c>PATHEXT</c> extensions (Windows semantics).</param>
    internal ExecutableResolver(IEnvironmentProvider environment, IFileSystem fileSystem, char pathSeparator, bool probePathExtensions)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _environment = environment;
        _fileSystem = fileSystem;
        _pathSeparator = pathSeparator;
        _probePathExtensions = probePathExtensions;
    }

    /// <summary>
    /// Determines whether <paramref name="toolName"/> is a bare name: not blank, not rooted,
    /// free of directory separators and free of <c>..</c>.
    /// </summary>
    /// <param name="toolName">The candidate tool name.</param>
    /// <returns><see langword="true"/> when the name is bare; otherwise <see langword="false"/>.</returns>
    internal static bool IsBareName(string? toolName) =>
        !string.IsNullOrWhiteSpace(toolName)
        && !Path.IsPathRooted(toolName)
        && toolName.IndexOfAny(['/', '\\']) < 0
        && !toolName.Contains("..", StringComparison.Ordinal);

    /// <inheritdoc />
    public string? Resolve(string toolName)
    {
        if (!IsBareName(toolName))
        {
            return null;
        }

        var path = _environment.GetEnvironmentVariable(PathVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        IReadOnlyList<string> suffixes = _probePathExtensions && !Path.HasExtension(toolName)
            ? GetPathExtensions()
            : [string.Empty];

        foreach (var rawSegment in path.Split(_pathSeparator, SplitOptions))
        {
            var directory = NormaliseSegment(rawSegment);
            if (!Path.IsPathFullyQualified(directory))
            {
                continue;
            }

            foreach (var suffix in suffixes)
            {
                var candidate = Path.Combine(directory, toolName + suffix);
                if (_fileSystem.FileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string NormaliseSegment(string segment)
    {
        var isQuoted = segment.Length >= 2 && segment[0] == Quote && segment[^1] == Quote;
        return isQuoted ? segment[1..^1].Trim() : segment;
    }

    private List<string> GetPathExtensions()
    {
        var raw = _environment.GetEnvironmentVariable(PathExtVariable) ?? string.Empty;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extensions = new List<string>();

        foreach (var entry in raw.Split(PathExtSeparator, SplitOptions))
        {
            var extension = entry.StartsWith('.') ? entry : "." + entry;
            if (seen.Add(extension))
            {
                extensions.Add(extension);
            }
        }

        return extensions.Count > 0 ? extensions : [.. DefaultExtensions];
    }
}
