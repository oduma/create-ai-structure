using System.Text;

namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>
/// An isolated temp tree for one real run of the published executable: a <c>shims</c> directory of offline
/// <c>.cmd</c> stand-ins for <c>git</c>, <c>gh</c>, <c>depviz</c> and <c>t3</c>, an empty <c>work</c> directory used as
/// the current directory, and empty sandbox directories used for the install-location and <c>gh</c> config variables.
/// </summary>
/// <remarks>
/// The shims make zero network calls: <c>gh repo create ... -- &lt;name&gt;</c> creates <c>&lt;cwd&gt;/&lt;name&gt;</c>,
/// <c>git remote ...</c> echoes <see cref="RemoteOriginUrl"/>, and every other invocation exits 0.
/// </remarks>
internal sealed class StubToolSandbox : IDisposable
{
    /// <summary>The <c>origin</c> URL reported by the <c>git</c> shim.</summary>
    public const string RemoteOriginUrl = "https://github.com/test-owner/standalone-demo.git";

    // gh repo create --public --clone -- <name>: the repository name is the sixth argument.
    private const string GhShim = """
        @echo off
        if /I "%~1"=="repo" if /I "%~2"=="create" mkdir "%~6" || exit /b 1
        exit /b 0
        """;

    private const string GitShim = $"""
        @echo off
        if /I "%~1"=="remote" echo {RemoteOriginUrl}
        exit /b 0
        """;

    private const string SucceedingShim = """
        @echo off
        exit /b 0
        """;

    private readonly TemporaryDirectory _root = new("stub-run");

    public StubToolSandbox()
    {
        ShimDirectory = _root.CreateSubdirectory("shims");
        WorkingDirectory = _root.CreateSubdirectory("work");
        LocalApplicationDataDirectory = _root.CreateSubdirectory("localappdata");
        ProgramFilesDirectory = _root.CreateSubdirectory("programfiles");
        ProgramFilesX86Directory = _root.CreateSubdirectory("programfiles-x86");
        GhConfigDirectory = _root.CreateSubdirectory("gh-config");
        MissingT3DesktopExecutable = Path.Combine(_root.FullPath, "missing", "T3 Code.exe");

        WriteShim("gh", GhShim);
        WriteShim("git", GitShim);
        WriteShim("depviz", SucceedingShim);
        WriteShim("t3", SucceedingShim);
    }

    /// <summary>Gets the directory holding the <c>.cmd</c> shims.</summary>
    public string ShimDirectory { get; }

    /// <summary>Gets the empty directory used as the current directory of the run.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Gets the empty directory used as <c>LOCALAPPDATA</c>.</summary>
    public string LocalApplicationDataDirectory { get; }

    /// <summary>Gets the empty directory used as <c>ProgramFiles</c>.</summary>
    public string ProgramFilesDirectory { get; }

    /// <summary>Gets the empty directory used as <c>ProgramFiles(x86)</c>.</summary>
    public string ProgramFilesX86Directory { get; }

    /// <summary>Gets the empty directory used as <c>GH_CONFIG_DIR</c>, so no real <c>gh</c> login is ever read.</summary>
    public string GhConfigDirectory { get; }

    /// <summary>Gets a non-existent sandbox path used as <c>T3CODE_DESKTOP_EXE</c>.</summary>
    public string MissingT3DesktopExecutable { get; }

    /// <summary>
    /// Gets the environment overrides for the run: <c>PATH</c> limited to the shims and <c>%SystemRoot%\System32</c>
    /// (for <c>cmd.exe</c>), Windows <c>PATHEXT</c>, every GitHub credential/host variable removed, <c>GH_CONFIG_DIR</c>
    /// pointing at an empty sandbox directory, <c>T3CODE_DESKTOP_EXE</c> pointing at a non-existent sandbox file (the
    /// locator honours an existing override first, so this neutralises any real one), and empty sandbox directories as
    /// <c>LOCALAPPDATA</c>, <c>ProgramFiles</c> and <c>ProgramFiles(x86)</c>.
    /// </summary>
    /// <remarks>
    /// The desktop locator reads <c>ProgramFiles</c>/<c>ProgramFiles(x86)</c> from the environment, so those overrides are
    /// effective; it resolves the per-user folder through the Windows known-folder API, which ignores the
    /// <c>LOCALAPPDATA</c> variable, so that override is defense-in-depth only. The real guarantee that no T3 GUI is
    /// started is the <c>t3</c> shim exiting 0, which keeps the launcher off its cold-start path (asserted by the test).
    /// </remarks>
    public IReadOnlyDictionary<string, string?> Environment => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["PATH"] = $"{ShimDirectory};{System.Environment.GetFolderPath(System.Environment.SpecialFolder.System)}",
        ["PATHEXT"] = ".COM;.EXE;.BAT;.CMD",
        ["LOCALAPPDATA"] = LocalApplicationDataDirectory,
        ["ProgramFiles"] = ProgramFilesDirectory,
        ["ProgramFiles(x86)"] = ProgramFilesX86Directory,
        ["T3CODE_DESKTOP_EXE"] = MissingT3DesktopExecutable,
        ["GH_CONFIG_DIR"] = GhConfigDirectory,
        ["GH_TOKEN"] = null,
        ["GITHUB_TOKEN"] = null,
        ["GH_ENTERPRISE_TOKEN"] = null,
        ["GITHUB_ENTERPRISE_TOKEN"] = null,
        ["GH_HOST"] = null,
    };

    public void Dispose() => _root.Dispose();

    private void WriteShim(string toolName, string script) =>
        File.WriteAllText(
            Path.Combine(ShimDirectory, toolName + ".cmd"),
            script.ReplaceLineEndings("\r\n") + "\r\n",
            Encoding.ASCII);
}
