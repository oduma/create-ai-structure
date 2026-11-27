using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.Process;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CreateAiStructure.Infrastructure.Tests.Process;

public class ProcessRunnerTests
{
    private const string BareName = "tool";

    private static readonly string ResolvedPath = Path.Combine(Path.GetTempPath(), "car-runner-tests", "bin", "tool.exe");
    private static readonly string FullyQualifiedPath = Path.Combine(Path.GetTempPath(), "car-runner-tests", "fq", "other.exe");

    private readonly IExecutableResolver _resolver = Substitute.For<IExecutableResolver>();
    private readonly IProcessStarter _starter = Substitute.For<IProcessStarter>();
    private readonly IProcessHandle _handle = Substitute.For<IProcessHandle>();

    private ProcessStartInfo? _captured;

    // ----- helpers -------------------------------------------------------------------------

    private ProcessRunner CreateRunner() => new(_resolver, _starter);

    private static ProcessExecutionRequest Request(string fileName, string? workingDirectory = null, params string[] arguments) =>
        new(fileName, arguments, workingDirectory);

    private void GivenResolves(string toolName, string? path) => _resolver.Resolve(toolName).Returns(path);

    private void GivenStarterReturnsHandle() =>
        _starter.Start(Arg.Do<ProcessStartInfo>(psi => _captured = psi)).Returns(_handle);

    private void GivenHandleCompletes(int exitCode, string stdout, string stderr)
    {
        _handle.ReadStandardOutputToEndAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(stdout));
        _handle.ReadStandardErrorToEndAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(stderr));
        _handle.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _handle.HasExited.Returns(true);
        _handle.ExitCode.Returns(exitCode);
    }

    private void GivenWaitIsCancelled(bool hasExited)
    {
        _handle.ReadStandardOutputToEndAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(string.Empty));
        _handle.ReadStandardErrorToEndAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(string.Empty));
        _handle.WaitForExitAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new OperationCanceledException()));
        _handle.HasExited.Returns(hasExited);
    }

    private void AssertStarterNotCalled() => _starter.DidNotReceiveWithAnyArgs().Start(default!);

    // ----- constructor guards --------------------------------------------------------------

    [Fact]
    public void Internal_ctor_throws_when_resolver_is_null()
    {
        var act = () => new ProcessRunner(null!, _starter);

        act.Should().Throw<ArgumentNullException>().WithParameterName("resolver");
    }

    [Fact]
    public void Internal_ctor_throws_when_starter_is_null()
    {
        var act = () => new ProcessRunner(_resolver, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("starter");
    }

    [Fact]
    public void Runner_implements_IProcessRunner()
    {
        CreateRunner().Should().BeAssignableTo<IProcessRunner>();
    }

    // ----- argument / pre-cancellation guards ---------------------------------------------

    [Fact]
    public async Task RunAsync_throws_when_request_is_null()
    {
        var act = () => CreateRunner().RunAsync(null!);

        (await act.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("request");
        _resolver.DidNotReceiveWithAnyArgs().Resolve(default!);
        AssertStarterNotCalled();
    }

    [Fact]
    public async Task RunAsync_throws_when_token_is_already_cancelled_without_resolving_or_starting()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => CreateRunner().RunAsync(Request(BareName), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _resolver.DidNotReceiveWithAnyArgs().Resolve(default!);
        AssertStarterNotCalled();
    }

    // ----- executable selection -----------------------------------------------------------

    [Fact]
    public async Task Bare_name_is_resolved_and_start_info_is_hardened()
    {
        GivenResolves(BareName, ResolvedPath);
        GivenStarterReturnsHandle();
        GivenHandleCompletes(0, "out", "err");
        string[] arguments = ["plain", "has space", "a\"b", "&|;$", string.Empty];

        await CreateRunner().RunAsync(Request(BareName, null, arguments));

        _captured.Should().NotBeNull();
        _captured!.FileName.Should().Be(ResolvedPath);
        _captured.UseShellExecute.Should().BeFalse();
        _captured.CreateNoWindow.Should().BeTrue();
        _captured.RedirectStandardOutput.Should().BeTrue();
        _captured.RedirectStandardError.Should().BeTrue();
        _captured.RedirectStandardInput.Should().BeFalse();
        _captured.StandardOutputEncoding.Should().Be(Encoding.UTF8);
        _captured.StandardErrorEncoding.Should().Be(Encoding.UTF8);
        _captured.Arguments.Should().BeEmpty();
        _captured.ArgumentList.Should().Equal(arguments);
        _resolver.Received(1).Resolve(BareName);
    }

    [Fact]
    public async Task Bare_name_not_found_returns_failure_without_starting()
    {
        GivenResolves(BareName, null);

        var result = await CreateRunner().RunAsync(Request(BareName));

        result.Should().Be(new ProcessExecutionResult(-1, string.Empty, "Executable 'tool' was not found on PATH."));
        AssertStarterNotCalled();
    }

    [Fact]
    public async Task Fully_qualified_path_is_used_verbatim_without_resolving()
    {
        GivenStarterReturnsHandle();
        GivenHandleCompletes(0, string.Empty, string.Empty);

        await CreateRunner().RunAsync(Request(FullyQualifiedPath));

        _captured!.FileName.Should().Be(FullyQualifiedPath);
        _resolver.DidNotReceiveWithAnyArgs().Resolve(default!);
    }

    [Theory]
    [InlineData(".\\tool.exe")]
    [InlineData("./tool")]
    [InlineData("../tool")]
    [InlineData("bin/tool")]
    [InlineData("bin\\tool")]
    [InlineData("..")]
    public async Task Relative_paths_are_refused(string fileName)
    {
        var result = await CreateRunner().RunAsync(Request(fileName));

        result.Should().Be(new ProcessExecutionResult(
            -1,
            string.Empty,
            $"Relative executable path '{fileName}' is not permitted; specify a command name on PATH or a fully qualified path."));
        _resolver.DidNotReceiveWithAnyArgs().Resolve(default!);
        AssertStarterNotCalled();
    }

    [Fact]
    public async Task Drive_relative_path_is_refused_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string fileName = "C:tool.exe";

        var result = await CreateRunner().RunAsync(Request(fileName));

        result.ExitCode.Should().Be(-1);
        result.StandardError.Should().Be(
            $"Relative executable path '{fileName}' is not permitted; specify a command name on PATH or a fully qualified path.");
        AssertStarterNotCalled();
    }

    // ----- working directory --------------------------------------------------------------

    [Fact]
    public async Task Working_directory_is_set_when_provided()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "car-runner-tests", "work");
        GivenStarterReturnsHandle();
        GivenHandleCompletes(0, string.Empty, string.Empty);

        await CreateRunner().RunAsync(Request(FullyQualifiedPath, workingDirectory));

        _captured!.WorkingDirectory.Should().Be(workingDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Working_directory_is_left_empty_when_null_or_whitespace(string? workingDirectory)
    {
        GivenStarterReturnsHandle();
        GivenHandleCompletes(0, string.Empty, string.Empty);

        await CreateRunner().RunAsync(Request(FullyQualifiedPath, workingDirectory));

        _captured!.WorkingDirectory.Should().BeEmpty();
    }

    // ----- start failures -----------------------------------------------------------------

    [Fact]
    public async Task Start_throwing_Win32Exception_returns_failure_result()
    {
        _starter.Start(Arg.Any<ProcessStartInfo>()).Throws(new Win32Exception(2, "win32 boom"));

        var result = await CreateRunner().RunAsync(Request(FullyQualifiedPath));

        result.Should().Be(new ProcessExecutionResult(-1, string.Empty, $"Failed to start '{FullyQualifiedPath}': win32 boom"));
    }

    [Fact]
    public async Task Start_throwing_FileNotFoundException_returns_failure_result()
    {
        _starter.Start(Arg.Any<ProcessStartInfo>()).Throws(new FileNotFoundException("fnf boom"));

        var result = await CreateRunner().RunAsync(Request(FullyQualifiedPath));

        result.Should().Be(new ProcessExecutionResult(-1, string.Empty, $"Failed to start '{FullyQualifiedPath}': fnf boom"));
    }

    [Fact]
    public async Task Start_returning_null_returns_failure_result()
    {
        _starter.Start(Arg.Any<ProcessStartInfo>()).Returns((IProcessHandle?)null);

        var result = await CreateRunner().RunAsync(Request(FullyQualifiedPath));

        result.Should().Be(new ProcessExecutionResult(-1, string.Empty, $"Failed to start '{FullyQualifiedPath}'."));
    }

    [Fact]
    public async Task Start_throwing_an_unrelated_exception_propagates()
    {
        _starter.Start(Arg.Any<ProcessStartInfo>()).Throws(new InvalidOperationException("unexpected"));

        var act = () => CreateRunner().RunAsync(Request(FullyQualifiedPath));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("unexpected");
    }

    // ----- successful runs ----------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-5)]
    public async Task Completed_run_returns_exit_code_and_output_and_disposes_handle(int exitCode)
    {
        GivenResolves(BareName, ResolvedPath);
        GivenStarterReturnsHandle();
        GivenHandleCompletes(exitCode, "stdout ✓", "stderr ✗");
        using var cts = new CancellationTokenSource();

        var result = await CreateRunner().RunAsync(Request(BareName), cts.Token);

        result.Should().Be(new ProcessExecutionResult(exitCode, "stdout ✓", "stderr ✗"));
        await _handle.Received(1).WaitForExitAsync(cts.Token);
        _handle.DidNotReceive().KillEntireProcessTree();
        _handle.Received(1).Dispose();
    }

    [Fact]
    public async Task Output_reads_are_started_before_waiting_for_exit()
    {
        GivenStarterReturnsHandle();
        GivenHandleCompletes(0, "o", "e");
        var readsStartedBeforeWait = false;
        _handle.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var names = _handle.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ToList();
            readsStartedBeforeWait = names.Contains(nameof(IProcessHandle.ReadStandardOutputToEndAsync))
                && names.Contains(nameof(IProcessHandle.ReadStandardErrorToEndAsync));
            return Task.CompletedTask;
        });

        await CreateRunner().RunAsync(Request(FullyQualifiedPath));

        readsStartedBeforeWait.Should().BeTrue();
    }

    // ----- cancellation mid-run -----------------------------------------------------------

    [Fact]
    public async Task Cancellation_while_running_kills_tree_rethrows_and_disposes()
    {
        GivenStarterReturnsHandle();
        GivenWaitIsCancelled(hasExited: false);

        var act = () => CreateRunner().RunAsync(Request(FullyQualifiedPath));

        await act.Should().ThrowAsync<OperationCanceledException>();
        _handle.Received(1).KillEntireProcessTree();
        _handle.Received(1).Dispose();
    }

    [Fact]
    public async Task Cancellation_after_process_exited_does_not_kill_but_rethrows_and_disposes()
    {
        GivenStarterReturnsHandle();
        GivenWaitIsCancelled(hasExited: true);

        var act = () => CreateRunner().RunAsync(Request(FullyQualifiedPath));

        await act.Should().ThrowAsync<OperationCanceledException>();
        _handle.DidNotReceive().KillEntireProcessTree();
        _handle.Received(1).Dispose();
    }

    [Fact]
    public async Task Cancellation_propagates_the_original_exception_instance()
    {
        var original = new OperationCanceledException("original");
        GivenStarterReturnsHandle();
        GivenWaitIsCancelled(hasExited: false);
        _handle.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(original));

        var act = () => CreateRunner().RunAsync(Request(FullyQualifiedPath));

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.Should().BeSameAs(original);
    }
}
