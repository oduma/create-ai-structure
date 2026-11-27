using System.Text;
using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// A test-local <see cref="IFileSystem"/> over <see cref="System.IO"/> used by the end-to-end disk tests.
/// Writes use UTF-8 without a byte order mark.
/// </summary>
internal sealed class TestUtf8NoBomFileSystem : IFileSystem
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public IReadOnlyList<string> GetFiles(string directoryPath, string searchPattern) =>
        Directory.GetFiles(directoryPath, searchPattern);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public Task WriteAllTextUtf8NoBomAsync(string path, string content, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, content, Utf8NoBom, cancellationToken);
}
