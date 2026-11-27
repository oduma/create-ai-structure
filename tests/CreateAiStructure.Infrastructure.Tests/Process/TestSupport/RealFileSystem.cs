using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Tests.Process.TestSupport;

/// <summary>Test-only thin wrapper over <see cref="File"/>/<see cref="Directory"/> for real-process smoke tests.</summary>
internal sealed class RealFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public IReadOnlyList<string> GetFiles(string directoryPath, string searchPattern) => throw new NotSupportedException();

    public void CreateDirectory(string path) => throw new NotSupportedException();

    public Task WriteAllTextUtf8NoBomAsync(string path, string content, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
