using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.Process;
using CreateAiStructure.Infrastructure.Tests.Process.TestSupport;
using FluentAssertions;
using SysProcess = System.Diagnostics.Process;

namespace CreateAiStructure.Infrastructure.Tests.Process;

/// <summary>
/// Real-process smoke tests for <see cref="ProcessRunner"/> and the concrete OS adapters
/// (<see cref="SystemProcessStarter"/>, <see cref="SystemProcessHandle"/>).
/// Relies only on <c>dotnet</c> being on PATH plus OS-shipped <c>ping.exe</c> / <c>/bin/sleep</c>.
/// </summary>
[Trait("Category", "Smoke")]
public class ProcessRunnerSmokeTests
{
    private const string DotNet = "dotnet";
    private static readonly TimeSpan CancelAfter = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CancellationBound = TimeSpan.FromSeconds(15);

    // ----- helpers -------------------------------------------------------------------------

    private static ProcessRunner CreateRunner() => new(new RealEnvironmentProvider(), new RealFileSystem());

    private static string ResolveDotNet()
    {
        var path = new ExecutableResolver(new RealEnvironmentProvider(), new RealFileSystem()).Resolve(DotNet);
        path.Should().NotBeNull("dotnet must be available on PATH for smoke tests");
        return path!;
    }

    private static ProcessStartInfo HardenedStartInfo(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string NonExistentFullyQualifiedExecutable() =>
        Path.Combine(Path.GetTempPath(), $"car-missing-{Guid.NewGuid():N}", OperatingSystem.IsWindows() ? "missing.exe" : "missing");

    private static ProcessExecutionRequest LongRunningRequest() =>
        OperatingSystem.IsWindows()
            ? new ProcessExecutionRequest(Path.Combine(Environment.SystemDirectory, "ping.exe"), ["-n", "30", "127.0.0.1"])
            : new ProcessExecutionRequest("/bin/sleep", ["30"]);

    // ----- ProcessRunner via public ctor --------------------------------------------------

    [Fact]
    public void Public_ctor_constructs_an_IProcessRunner()
    {
        CreateRunner().Should().BeAssignableTo<IProcessRunner>();
    }

    [Fact]
    public async Task Dotnet_version_via_bare_name_succeeds_with_stdout_and_empty_stderr()
    {
        var result = await CreateRunner().RunAsync(new ProcessExecutionRequest(DotNet, ["--version"]));

        result.ExitCode.Should().Be(0);
        result.Succeeded.Should().BeTrue();
        result.StandardOutput.Trim().Should().NotBeEmpty();
        result.StandardError.Should().BeEmpty();
    }

    [Fact]
    public async Task Dotnet_unknown_command_returns_non_zero_exit_and_captures_output()
    {
        var result = await CreateRunner().RunAsync(new ProcessExecutionRequest(DotNet, ["nonexistent-command-xyz"]));

        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().NotBeNull();
        result.StandardError.Should().NotBeNull();
        (result.StandardOutput + result.StandardError).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Bare_name_not_on_path_returns_not_found_result()
    {
        var name = $"car-no-such-tool-{Guid.NewGuid():N}";

        var result = await CreateRunner().RunAsync(new ProcessExecutionRequest(name, []));

        result.Should().Be(new ProcessExecutionResult(-1, string.Empty, $"Executable '{name}' was not found on PATH."));
    }

    [Fact]
    public async Task Fully_qualified_non_existent_executable_returns_start_failure()
    {
        var missing = NonExistentFullyQualifiedExecutable();

        var result = await CreateRunner().RunAsync(new ProcessExecutionRequest(missing, []));

        result.ExitCode.Should().Be(-1);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().StartWith($"Failed to start '{missing}'").And.Contain(missing);
    }

    [Fact]
    public async Task Working_directory_is_honoured()
    {
        var workingDirectory = Directory.CreateTempSubdirectory("car-runner-wd-");
        try
        {
            var result = await CreateRunner().RunAsync(
                new ProcessExecutionRequest(DotNet, ["--version"], workingDirectory.FullName));

            result.ExitCode.Should().Be(0);
            result.StandardOutput.Trim().Should().NotBeEmpty();
        }
        finally
        {
            workingDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Cancelling_a_long_running_process_kills_it_and_throws_promptly()
    {
        using var cts = new CancellationTokenSource(CancelAfter);
        var stopwatch = Stopwatch.StartNew();

        var act = () => CreateRunner().RunAsync(LongRunningRequest(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Elapsed.Should().BeLessThan(CancellationBound);
    }

    // ----- SystemProcessStarter / SystemProcessHandle directly ----------------------------

    [Fact]
    public async Task SystemProcessStarter_starts_a_real_process_and_handle_exposes_its_outcome()
    {
        var starter = new SystemProcessStarter();

        using var handle = starter.Start(HardenedStartInfo(ResolveDotNet(), "--version"));

        handle.Should().NotBeNull().And.BeOfType<SystemProcessHandle>();
        var stdout = handle!.ReadStandardOutputToEndAsync(CancellationToken.None);
        var stderr = handle.ReadStandardErrorToEndAsync(CancellationToken.None);
        await handle.WaitForExitAsync(CancellationToken.None);

        (await stdout).Trim().Should().NotBeEmpty();
        (await stderr).Should().BeEmpty();
        handle.HasExited.Should().BeTrue();
        handle.ExitCode.Should().Be(0);
    }

    [Fact]
    public void SystemProcessStarter_throws_Win32Exception_for_missing_executable()
    {
        var starter = new SystemProcessStarter();

        var act = () => starter.Start(HardenedStartInfo(NonExistentFullyQualifiedExecutable()));

        act.Should().Throw<Win32Exception>();
    }

    [Fact]
    public async Task KillEntireProcessTree_on_an_already_exited_process_does_not_throw()
    {
        using var handle = new SystemProcessStarter().Start(HardenedStartInfo(ResolveDotNet(), "--version"))!;
        var stdout = handle.ReadStandardOutputToEndAsync(CancellationToken.None);
        var stderr = handle.ReadStandardErrorToEndAsync(CancellationToken.None);
        await handle.WaitForExitAsync(CancellationToken.None);
        await Task.WhenAll(stdout, stderr);
        handle.HasExited.Should().BeTrue();

        var act = handle.KillEntireProcessTree;

        act.Should().NotThrow();
    }

    [Fact]
    public void KillEntireProcessTree_swallows_InvalidOperationException_when_no_process_is_associated()
    {
        // An unstarted Process makes Kill(entireProcessTree: true) throw InvalidOperationException
        // deterministically, exercising the same catch as the "already exited" race.
        using var handle = new SystemProcessHandle(new SysProcess());

        var act = handle.KillEntireProcessTree;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SystemProcessHandle_kills_a_running_process_tree()
    {
        var request = LongRunningRequest();
        using var handle = new SystemProcessStarter().Start(HardenedStartInfo(request.FileName, [.. request.Arguments]))!;
        handle.HasExited.Should().BeFalse();

        handle.KillEntireProcessTree();

        using var cts = new CancellationTokenSource(CancellationBound);
        await handle.WaitForExitAsync(cts.Token);
        handle.HasExited.Should().BeTrue();
    }

    [Fact]
    public async Task SystemProcessHandle_WaitForExitAsync_honours_cancellation()
    {
        var request = LongRunningRequest();
        using var handle = new SystemProcessStarter().Start(HardenedStartInfo(request.FileName, [.. request.Arguments]))!;
        try
        {
            using var cts = new CancellationTokenSource(CancelAfter);

            var act = () => handle.WaitForExitAsync(cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            handle.KillEntireProcessTree();
        }
    }
}
