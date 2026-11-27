using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Cli.Tests.EndToEnd.TestSupport;

/// <summary>
/// A scripted <see cref="IProcessRunner"/> that never starts a real process. It records every call in order and
/// simulates the external tools the scaffolding pipeline expects:
/// <list type="bullet">
/// <item><c>gh repo create … --clone -- &lt;repo&gt;</c> creates <c>&lt;cwd&gt;/&lt;repo&gt;</c> and succeeds;</item>
/// <item><c>git remote get-url origin</c> prints <c>https://github.com/test-owner/&lt;repo&gt;.git</c>;</item>
/// <item>the remaining expected <c>git</c>, <c>gh label create</c>, <c>depviz init</c> and <c>t3 app</c> calls succeed
/// (<c>t3 app</c> succeeds outright, i.e. the warm path);</item>
/// <item>anything else fails with exit code 127 so that an unexpected command surfaces as a test failure.</item>
/// </list>
/// </summary>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    /// <summary>The owner used in the simulated <c>origin</c> remote URL.</summary>
    public const string RemoteOwner = "test-owner";

    private readonly List<ProcessExecutionRequest> _calls = [];
    private readonly Lock _gate = new();

    /// <summary>Gets a snapshot of the recorded calls, in invocation order.</summary>
    public IReadOnlyList<ProcessExecutionRequest> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _calls.Add(request);
        }

        return Task.FromResult(Respond(request));
    }

    private static ProcessExecutionResult Respond(ProcessExecutionRequest request)
    {
        var args = request.Arguments;
        var command = $"{request.FileName} {string.Join(' ', args)}";

        switch (request.FileName)
        {
            case "gh" when StartsWith(args, "repo", "create") && args.Contains("--clone"):
                Directory.CreateDirectory(Path.Combine(request.WorkingDirectory!, args[^1]));
                return Success();

            case "gh" when StartsWith(args, "label", "create"):
            case "git" when StartsWith(args, "branch", "-M", "main"):
            case "git" when StartsWith(args, "add"):
            case "git" when StartsWith(args, "commit"):
            case "git" when StartsWith(args, "push"):
            case "depviz" when StartsWith(args, "init"):
                return Success();

            case "git" when StartsWith(args, "remote", "get-url", "origin"):
                var repository = Path.GetFileName(request.WorkingDirectory!);
                return Success($"https://github.com/{RemoteOwner}/{repository}.git\n");

            case "t3" when StartsWith(args, "app"):
                return Success("Opened the repository in T3 Code.\n");

            default:
                return new ProcessExecutionResult(127, string.Empty, $"Unscripted command: {command}");
        }
    }

    private static bool StartsWith(IReadOnlyList<string> args, params string[] prefix) =>
        args.Count >= prefix.Length && args.Take(prefix.Length).SequenceEqual(prefix, StringComparer.Ordinal);

    private static ProcessExecutionResult Success(string standardOutput = "") => new(0, standardOutput, string.Empty);
}
