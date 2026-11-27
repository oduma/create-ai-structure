namespace CreateAiStructure.Cli.Tests.EndToEnd.TestSupport;

/// <summary>
/// An isolated, uniquely named temp-directory tree for one end-to-end run, deleted recursively (best-effort) on dispose:
/// a <c>bin</c> directory holding empty placeholder executables for every required tool, an empty <c>work</c>
/// directory used as the current directory, and an empty <c>localappdata</c> directory.
/// </summary>
internal sealed class EndToEndSandbox : IDisposable
{
    /// <summary>The placeholder executables created in <see cref="ToolDirectory"/>.</summary>
    public static readonly IReadOnlyList<string> PlaceholderExecutables = ["git.exe", "gh.exe", "depviz.exe", "t3.exe"];

    public EndToEndSandbox()
    {
        Root = Path.Combine(Path.GetTempPath(), "cas-e2e-tests", Guid.NewGuid().ToString("N"));
        ToolDirectory = CreateSubdirectory("bin");
        WorkingDirectory = CreateSubdirectory("work");
        LocalApplicationDataDirectory = CreateSubdirectory("localappdata");

        foreach (var executable in PlaceholderExecutables)
        {
            File.WriteAllBytes(Path.Combine(ToolDirectory, executable), []);
        }
    }

    /// <summary>Gets the root of the sandbox tree.</summary>
    public string Root { get; }

    /// <summary>Gets the directory placed on the fake <c>PATH</c>.</summary>
    public string ToolDirectory { get; }

    /// <summary>Gets the directory reported as the current working directory.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Gets the directory reported as the local application data folder.</summary>
    public string LocalApplicationDataDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
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

    private string CreateSubdirectory(string name) => Directory.CreateDirectory(Path.Combine(Root, name)).FullName;
}
