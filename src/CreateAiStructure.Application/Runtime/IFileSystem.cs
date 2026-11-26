namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// Abstracts the file system operations the application needs.
/// </summary>
public interface IFileSystem
{
    /// <summary>Determines whether a directory exists.</summary>
    /// <param name="path">The directory path.</param>
    /// <returns><see langword="true"/> if the directory exists; otherwise <see langword="false"/>.</returns>
    bool DirectoryExists(string path);

    /// <summary>Determines whether a file exists.</summary>
    /// <param name="path">The file path.</param>
    /// <returns><see langword="true"/> if the file exists; otherwise <see langword="false"/>.</returns>
    bool FileExists(string path);

    /// <summary>Lists the files in a directory that match a search pattern.</summary>
    /// <param name="directoryPath">The directory to search.</param>
    /// <param name="searchPattern">The search pattern, for example <c>*.exe</c>.</param>
    /// <returns>The full paths of the matching files.</returns>
    IReadOnlyList<string> GetFiles(string directoryPath, string searchPattern);

    /// <summary>Creates a directory and any missing parent directories.</summary>
    /// <param name="path">The directory path.</param>
    void CreateDirectory(string path);

    /// <summary>Writes text to a file as UTF-8 without a byte order mark, replacing any existing content.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="content">The text to write.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    Task WriteAllTextUtf8NoBomAsync(string path, string content, CancellationToken cancellationToken = default);
}
