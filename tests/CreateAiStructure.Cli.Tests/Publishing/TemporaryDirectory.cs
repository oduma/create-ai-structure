namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>
/// A uniquely named directory under <c>%TEMP%/cas-publish-tests</c> (outside the repository), deleted recursively
/// (best-effort) on dispose.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    private const string ContainerName = "cas-publish-tests";

    public TemporaryDirectory(string purpose) =>
        FullPath = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), ContainerName, $"{purpose}-{Guid.NewGuid():N}")).FullName;

    /// <summary>Gets the absolute path of the directory.</summary>
    public string FullPath { get; }

    /// <summary>Creates (if needed) and returns the absolute path of a subdirectory.</summary>
    /// <param name="name">The subdirectory name.</param>
    /// <returns>The absolute subdirectory path.</returns>
    public string CreateSubdirectory(string name) => Directory.CreateDirectory(Path.Combine(FullPath, name)).FullName;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(FullPath))
            {
                Directory.Delete(FullPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup: a file still locked by an exiting process must not fail the test.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup: a read-only file must not fail the test.
        }
    }
}
