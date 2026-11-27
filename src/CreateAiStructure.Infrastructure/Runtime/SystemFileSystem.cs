using System.Text;
using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Runtime;

/// <summary>
/// Implements <see cref="IFileSystem"/> over the real file system via <see cref="File"/> and <see cref="Directory"/>.
/// </summary>
public sealed class SystemFileSystem : IFileSystem
{
    /// <summary>UTF-8 encoding that never emits a byte order mark.</summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="directoryPath"/> or <paramref name="searchPattern"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="directoryPath"/> or <paramref name="searchPattern"/> is empty or whitespace.</exception>
    public IReadOnlyList<string> GetFiles(string directoryPath, string searchPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        return Directory.GetFiles(directoryPath, searchPattern);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    public void CreateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task WriteAllTextUtf8NoBomAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        return File.WriteAllTextAsync(path, content, Utf8NoBom, cancellationToken);
    }
}
