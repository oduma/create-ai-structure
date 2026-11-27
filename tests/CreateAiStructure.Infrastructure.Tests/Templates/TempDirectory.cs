namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// A uniquely named directory under <see cref="Path.GetTempPath"/> that is deleted recursively, best-effort, on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cas-disk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the full path of the directory.</summary>
    public string Path { get; }

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
