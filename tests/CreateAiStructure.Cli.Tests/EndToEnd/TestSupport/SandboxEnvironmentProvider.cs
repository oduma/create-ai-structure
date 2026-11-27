using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Cli.Tests.EndToEnd.TestSupport;

/// <summary>
/// An <see cref="IEnvironmentProvider"/> confined to an <see cref="EndToEndSandbox"/>, so the end-to-end test never
/// reads or mutates the process-wide <c>PATH</c>, current directory or local application data folder (parallel-safe).
/// </summary>
internal sealed class SandboxEnvironmentProvider : IEnvironmentProvider
{
    private readonly EndToEndSandbox _sandbox;

    public SandboxEnvironmentProvider(EndToEndSandbox sandbox) => _sandbox = sandbox;

    public string? GetEnvironmentVariable(string name) => name.ToUpperInvariant() switch
    {
        "PATH" => _sandbox.ToolDirectory,
        "PATHEXT" => ".COM;.EXE;.BAT;.CMD",
        "LOCALAPPDATA" => _sandbox.LocalApplicationDataDirectory,
        _ => null,
    };

    public string GetLocalApplicationDataPath() => _sandbox.LocalApplicationDataDirectory;

    public string GetCurrentDirectory() => _sandbox.WorkingDirectory;
}
