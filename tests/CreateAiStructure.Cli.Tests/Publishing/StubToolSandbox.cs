using System.Text;

namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>
/// An isolated temp tree for one real scaffold run of the published executable: a <c>shims</c> directory of offline
/// <c>.cmd</c> stand-ins for <c>git</c>, <c>gh</c>, <c>depviz</c> and <c>t3</c>, an empty <c>work</c> directory used as
/// the current directory and an empty <c>localappdata</c> directory used for the install-location variables.
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

    /// <summary>
    /// Gets the environment overrides for the run: <c>PATH</c> limited to the shims and <c>%SystemRoot%\System32</c>
    /// (for <c>cmd.exe</c>), Windows <c>PATHEXT</c>, no T3 desktop override, and the empty directory as
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
        ["ProgramFiles"] = LocalApplicationDataDirectory,
        ["ProgramFiles(x86)"] = LocalApplicationDataDirectory,
        ["T3CODE_DESKTOP_EXE"] = null,
    };

    public void Dispose() => _root.Dispose();

    private void WriteShim(string toolName, string script) =>
        File.WriteAllText(
            Path.Combine(ShimDirectory, toolName + ".cmd"),
            script.ReplaceLineEndings("\r\n") + "\r\n",
            Encoding.ASCII);
}
