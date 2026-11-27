using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Locates the Windows T3 Code desktop executable from the <c>T3CODE_DESKTOP_EXE</c> override or,
/// failing that, from the standard per-user and machine-wide install directories. All environment
/// and file system access goes through the injected abstractions.
/// </summary>
public sealed class T3DesktopExecutableLocator : IT3DesktopExecutableLocator
{
    /// <summary>The environment variable that explicitly points to the desktop executable.</summary>
    private const string OverrideVariable = "T3CODE_DESKTOP_EXE";

    /// <summary>The environment variable holding the 64-bit Program Files directory.</summary>
    private const string ProgramFilesVariable = "ProgramFiles";

    /// <summary>The environment variable holding the 32-bit Program Files directory.</summary>
    private const string ProgramFilesX86Variable = "ProgramFiles(x86)";

    /// <summary>The search pattern used by the fallback executable scan.</summary>
    private const string ExecutableSearchPattern = "*.exe";

    /// <summary>The error code returned when no executable can be found.</summary>
    private const string NotFoundErrorCode = "T3Launch.DesktopExecutableNotFound";

    /// <summary>The character that may surround the override value.</summary>
    private const char Quote = '"';

    /// <summary>The file name fragment every fallback candidate must contain.</summary>
    private const string RequiredNameFragment = "t3";

    /// <summary>Known executable names, in priority order.</summary>
    private static readonly string[] ExactExecutableNames =
    [
        "T3 Code.exe",
        "T3 Code (Beta).exe",
        "t3code.exe",
        "T3Code.exe",
    ];

    /// <summary>File name fragments that identify uninstallers, updaters and elevation helpers.</summary>
    private static readonly string[] ExcludedNameFragments = ["uninstall", "update", "elevate"];

    private readonly IEnvironmentProvider _environmentProvider;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="T3DesktopExecutableLocator"/> class.
    /// </summary>
    /// <param name="environmentProvider">Provides environment variables and well-known folders.</param>
    /// <param name="fileSystem">Provides file and directory probing.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="environmentProvider"/> or <paramref name="fileSystem"/> is <see langword="null"/>.
    /// </exception>
    public T3DesktopExecutableLocator(IEnvironmentProvider environmentProvider, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(environmentProvider);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _environmentProvider = environmentProvider;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public Result<string> Locate()
    {
        var overridePath = ReadOverridePath();
        if (T3PathGuard.IsSafeAbsolutePath(overridePath) && _fileSystem.FileExists(overridePath))
        {
            return Result<string>.Success(overridePath);
        }

        var directories = GetInstallDirectories();
        var executable = FindInDirectories(directories);
        return executable is null
            ? Result<string>.Failure(CreateNotFoundError(directories))
            : Result<string>.Success(executable);
    }

    /// <summary>
    /// Reads the override variable, trimming it and stripping one matching pair of surrounding quotes.
    /// </summary>
    /// <returns>The normalized override value, or an empty string when the variable is unset.</returns>
    private string ReadOverridePath()
    {
        var value = (_environmentProvider.GetEnvironmentVariable(OverrideVariable) ?? string.Empty).Trim();
        return value.Length >= 2 && value[0] == Quote && value[^1] == Quote
            ? value[1..^1].Trim()
            : value;
    }

    /// <summary>
    /// Builds the ordered, case-insensitively deduplicated install directories, skipping unsafe base paths.
    /// </summary>
    /// <returns>The install directories to probe, in precedence order.</returns>
    private List<string> GetInstallDirectories()
    {
        var localAppData = _environmentProvider.GetLocalApplicationDataPath();
        var programFiles = _environmentProvider.GetEnvironmentVariable(ProgramFilesVariable);
        var programFilesX86 = _environmentProvider.GetEnvironmentVariable(ProgramFilesX86Variable);

        (string? BasePath, string RelativePath)[] candidates =
        [
            (localAppData, Path.Combine("Programs", "t3code")),
            (localAppData, Path.Combine("Programs", "T3 Code")),
            (localAppData, "t3code"),
            (programFiles, "t3code"),
            (programFiles, "T3 Code"),
            (programFilesX86, "t3code"),
            (programFilesX86, "T3 Code"),
        ];

        return candidates
            .Where(candidate => T3PathGuard.IsSafeAbsolutePath(candidate.BasePath))
            .Select(candidate => Path.Combine(candidate.BasePath!, candidate.RelativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns the first executable found across the existing install directories.</summary>
    /// <param name="directories">The install directories, in precedence order.</param>
    /// <returns>The executable path, or <see langword="null"/> when none is found.</returns>
    private string? FindInDirectories(IEnumerable<string> directories) =>
        directories
            .Where(_fileSystem.DirectoryExists)
            .Select(FindInDirectory)
            .FirstOrDefault(executable => executable is not null);

    /// <summary>Finds an executable in one directory by exact name, then by fallback scan.</summary>
    /// <param name="directory">The existing directory to search.</param>
    /// <returns>The executable path, or <see langword="null"/> when none is found.</returns>
    private string? FindInDirectory(string directory) =>
        ExactExecutableNames
            .Select(name => Path.Combine(directory, name))
            .FirstOrDefault(_fileSystem.FileExists)
        ?? ScanDirectory(directory);

    /// <summary>
    /// Scans a directory for <c>*.exe</c> files that look like T3 Code and are not uninstallers or updaters,
    /// choosing deterministically between case variants.
    /// </summary>
    /// <param name="directory">The existing directory to scan.</param>
    /// <returns>The first matching executable path, or <see langword="null"/> when none matches.</returns>
    private string? ScanDirectory(string directory) =>
        _fileSystem.GetFiles(directory, ExecutableSearchPattern)
            .Where(IsFallbackCandidate)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>Determines whether a scanned file is an acceptable fallback executable.</summary>
    /// <param name="path">The scanned file path.</param>
    /// <returns><see langword="true"/> when the file name contains <c>t3</c> and no excluded fragment.</returns>
    private static bool IsFallbackCandidate(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Contains(RequiredNameFragment, StringComparison.OrdinalIgnoreCase)
            && !Array.Exists(
                ExcludedNameFragments,
                fragment => fileName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Creates the not-found error describing what was probed and how to recover.</summary>
    /// <param name="directories">The install directories that were probed.</param>
    /// <returns>The <c>T3Launch.DesktopExecutableNotFound</c> error.</returns>
    private static DomainError CreateNotFoundError(IReadOnlyCollection<string> directories)
    {
        var probed = directories.Count == 0
            ? "No installation directories were available to probe."
            : "Probed directories: " + string.Join("; ", directories) + ".";

        var message =
            "The T3 Code desktop executable could not be found. " + probed +
            " Install or start T3 Code desktop manually, or set " + OverrideVariable +
            " to the full path of the executable.";

        return new DomainError(NotFoundErrorCode, message);
    }
}
