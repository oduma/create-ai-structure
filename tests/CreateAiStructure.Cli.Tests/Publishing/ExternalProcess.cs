using System.Diagnostics;
using System.Text;

namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>The captured outcome of an <see cref="ExternalProcess"/> run.</summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything written to stdout.</param>
/// <param name="StandardError">Everything written to stderr.</param>
internal sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Gets a diagnostic description used as the assertion reason.</summary>
    public string Describe() => $"exit code {ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}";
}

/// <summary>
/// Runs a real child process without a shell: arguments go through <see cref="ProcessStartInfo.ArgumentList"/>,
/// stdout/stderr are redirected and drained concurrently, and the whole process tree is killed on timeout.
/// </summary>
internal static class ExternalProcess
{
    /// <summary>Runs <paramref name="fileName"/> and waits for it to exit.</summary>
    /// <param name="fileName">The fully qualified executable path.</param>
    /// <param name="arguments">The arguments, passed verbatim.</param>
    /// <param name="workingDirectory">The working directory of the child process.</param>
    /// <param name="timeout">How long to wait before killing the process tree and failing.</param>
    /// <param name="environment">
    /// Environment overrides applied on top of the inherited environment; a <see langword="null"/> value removes the variable.
    /// </param>
    /// <returns>The exit code and the captured output.</returns>
    /// <exception cref="TimeoutException">The process did not exit within <paramref name="timeout"/>.</exception>
    public static async Task<ExternalProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        process.StandardInput.Close();

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            await Task.WhenAll(standardOutput, standardError).WaitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'{Path.GetFileName(fileName)} {string.Join(' ', startInfo.ArgumentList)}' did not finish within {timeout}.");
        }

        return new ExternalProcessResult(process.ExitCode, await standardOutput, await standardError);
    }
}
