namespace CreateAiStructure.Infrastructure.Tests.Runtime;

/// <summary>
/// A uniquely named directory under <see cref="Path.GetTempPath"/> that is deleted recursively, best-effort, on dispose.
/// Each instance is unique, so tests using it are safe to run in parallel.
/// </summary>
internal sealed class RuntimeTempDirectory : IDisposable
{
    public RuntimeTempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cas-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the full path of the directory.</summary>
    public string Path { get; }

    /// <summary>Combines <paramref name="parts"/> beneath <see cref="Path"/>.</summary>
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup: a locked file must not fail the test.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup: a read-only file must not fail the test.
        }
    }
}
