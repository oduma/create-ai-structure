using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    private const string DrainTimeoutMessage =
        "Output capture timed out after the process exited; a child process may still hold the output stream.";

    private static readonly string ResolvedPath = Path.Combine(Path.GetTempPath(), "car-runner-tests", "bin", "tool.exe");
    private static readonly string FullyQualifiedPath = Path.Combine(Path.GetTempPath(), "car-runner-tests", "fq", "other.exe");
    private static readonly string BatchShimPath = Path.Combine(Path.GetTempPath(), "car-runner-tests", "bin", "gh.cmd");
    private static readonly TimeSpan TinyDrainTimeout = TimeSpan.FromMilliseconds(50);

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

    // ----- batch-file (.cmd/.bat) argument guard ------------------------------------------

    public static TheoryData<char> UnsafeBatchCharacters() =>
        ['"', '%', '!', '^', '&', '|', '<', '>', '(', ')', '\r', '\n'];

    [Theory]
    [MemberData(nameof(UnsafeBatchCharacters))]
    public async Task Batch_file_with_an_unsafe_argument_character_is_refused_without_starting(char unsafeCharacter)
    {
        GivenResolves(BareName, BatchShimPath);
        var secret = $"value{unsafeCharacter}SECRET";

        var result = await CreateRunner().RunAsync(Request(BareName, null, "safe", secret));

        result.Should().Be(new ProcessExecutionResult(
            -1,
            string.Empty,
            "Argument 1 contains characters that are unsafe to pass to the batch file 'tool'."));
        result.StandardError.Should().NotContain("SECRET");
        AssertStarterNotCalled();
    }

    [Theory]
    [InlineData("gh.CMD")]
    [InlineData("gh.Bat")]
    [InlineData("gh.bat")]
    [InlineData("gh.cmd")]
    public async Task Batch_file_detection_is_case_insensitive_for_fully_qualified_paths(string shimName)
    {
        var path = Path.Combine(Path.GetTempPath(), "car-runner-tests", "fq", shimName);

        var result = await CreateRunner().RunAsync(Request(path, null, "a&b"));

        result.Should().Be(new ProcessExecutionResult(
            -1,
            string.Empty,
            $"Argument 0 contains characters that are unsafe to pass to the batch file '{path}'."));
        AssertStarterNotCalled();
    }

    [Fact]
    public async Task Batch_file_with_only_safe_arguments_is_started_with_arguments_verbatim()
    {
        GivenResolves(BareName, BatchShimPath);
        GivenStarterReturnsHandle();
        GivenHandleCompletes(0, "ok", string.Empty);
        string[] arguments = ["label", "create", "has space", "stage:qa", "a/b", "--force", "v1.2", "x_y", "#5319E7", string.Empty];

        var result = await CreateRunner().RunAsync(Request(BareName, null, arguments));

        result.Should().Be(new ProcessExecutionResult(0, "ok", string.Empty));
        _captured!.FileName.Should().Be(BatchShimPath);
        _captured.ArgumentList.Should().Equal(arguments);
    }

    [Theory]
    [InlineData("tool.exe")]
    [InlineData("tool.cmdx")]
    [InlineData("tool")]
    [InlineData("cmd.exe")]
    public async Task Non_batch_executables_accept_batch_metacharacters(string executableName)
    {
        var path = Path.Combine(Path.GetTempPath(), "car-runner-tests", "fq", executableName);
        GivenStarterReturnsHandle();
        GivenHandleCompletes(0, string.Empty, string.Empty);
        string[] arguments = ["\"%!^&|<>()\r\n"];

        var result = await CreateRunner().RunAsync(Request(path, null, arguments));

        result.ExitCode.Should().Be(0);
        _captured!.ArgumentList.Should().Equal(arguments);
    }

    // ----- output drain after exit --------------------------------------------------------

    private ProcessRunner CreateRunner(TimeSpan drainTimeout) => new(_resolver, _starter, drainTimeout);

    private void GivenExitedWithReads(int exitCode, Task<string> stdout, Task<string> stderr)
    {
        _handle.ReadStandardOutputToEndAsync(Arg.Any<CancellationToken>()).Returns(stdout);
        _handle.ReadStandardErrorToEndAsync(Arg.Any<CancellationToken>()).Returns(stderr);
        _handle.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _handle.HasExited.Returns(true);
        _handle.ExitCode.Returns(exitCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Internal_ctor_throws_when_drain_timeout_is_not_positive(int milliseconds)
    {
        var act = () => new ProcessRunner(_resolver, _starter, TimeSpan.FromMilliseconds(milliseconds));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("outputDrainTimeout");
    }

    [Fact]
    public void Internal_ctor_with_drain_timeout_still_guards_null_seams()
    {
        var withoutResolver = () => new ProcessRunner(null!, _starter, TinyDrainTimeout);
        var withoutStarter = () => new ProcessRunner(_resolver, null!, TinyDrainTimeout);

        withoutResolver.Should().Throw<ArgumentNullException>().WithParameterName("resolver");
        withoutStarter.Should().Throw<ArgumentNullException>().WithParameterName("starter");
    }

    [Fact]
    public async Task Drain_timeout_with_both_streams_stuck_returns_exit_code_and_timeout_message()
    {
        GivenStarterReturnsHandle();
        GivenExitedWithReads(0, new TaskCompletionSource<string>().Task, new TaskCompletionSource<string>().Task);

        var result = await CreateRunner(TinyDrainTimeout).RunAsync(Request(FullyQualifiedPath));

        result.Should().Be(new ProcessExecutionResult(0, string.Empty, DrainTimeoutMessage));
        _handle.DidNotReceive().KillEntireProcessTree();
        _handle.Received(1).Dispose();
    }

    [Fact]
    public async Task Drain_timeout_keeps_completed_stdout_when_only_stderr_is_stuck()
    {
        GivenStarterReturnsHandle();
        GivenExitedWithReads(7, Task.FromResult("partial out"), new TaskCompletionSource<string>().Task);

        var result = await CreateRunner(TinyDrainTimeout).RunAsync(Request(FullyQualifiedPath));

        result.Should().Be(new ProcessExecutionResult(7, "partial out", DrainTimeoutMessage));
    }

    [Fact]
    public async Task Drain_timeout_appends_message_to_completed_stderr_when_only_stdout_is_stuck()
    {
        GivenStarterReturnsHandle();
        GivenExitedWithReads(3, new TaskCompletionSource<string>().Task, Task.FromResult("real error"));

        var result = await CreateRunner(TinyDrainTimeout).RunAsync(Request(FullyQualifiedPath));

        result.Should().Be(new ProcessExecutionResult(3, string.Empty, $"real error{Environment.NewLine}{DrainTimeoutMessage}"));
    }

    [Fact]
    public async Task Drain_timeout_treats_faulted_stream_as_empty()
    {
        GivenStarterReturnsHandle();
        GivenExitedWithReads(
            2,
            Task.FromException<string>(new IOException("broken pipe")),
            new TaskCompletionSource<string>().Task);

        var result = await CreateRunner(TinyDrainTimeout).RunAsync(Request(FullyQualifiedPath));

        result.Should().Be(new ProcessExecutionResult(2, string.Empty, DrainTimeoutMessage));
    }

    [Fact]
    public async Task Reads_that_complete_within_the_drain_timeout_are_returned()
    {
        var stdout = new TaskCompletionSource<string>();
        var stderr = new TaskCompletionSource<string>();
        GivenStarterReturnsHandle();
        GivenExitedWithReads(0, stdout.Task, stderr.Task);

        var run = CreateRunner(TimeSpan.FromMinutes(5)).RunAsync(Request(FullyQualifiedPath));
        stdout.SetResult("late out");
        stderr.SetResult("late err");

        (await run).Should().Be(new ProcessExecutionResult(0, "late out", "late err"));
    }

    [Fact]
    public async Task Read_failure_within_the_drain_timeout_propagates()
    {
        GivenStarterReturnsHandle();
        GivenExitedWithReads(0, Task.FromException<string>(new IOException("broken pipe")), Task.FromResult(string.Empty));

        var act = () => CreateRunner().RunAsync(Request(FullyQualifiedPath));

        await act.Should().ThrowAsync<IOException>().WithMessage("broken pipe");
        _handle.Received(1).Dispose();
    }

    [Fact]
    public async Task Cancellation_while_draining_output_rethrows_without_killing_an_exited_process()
    {
        GivenStarterReturnsHandle();
        GivenExitedWithReads(0, new TaskCompletionSource<string>().Task, new TaskCompletionSource<string>().Task);
        using var cts = new CancellationTokenSource();

        var run = CreateRunner(TimeSpan.FromMinutes(5)).RunAsync(Request(FullyQualifiedPath), cts.Token);
        await cts.CancelAsync();

        await FluentActions.Awaiting(() => run).Should().ThrowAsync<OperationCanceledException>();
        _handle.DidNotReceive().KillEntireProcessTree();
        _handle.Received(1).Dispose();
    }

    [Fact]
    public async Task Cancellation_while_draining_output_kills_tree_when_process_reports_not_exited()
    {
        GivenStarterReturnsHandle();
        GivenExitedWithReads(0, new TaskCompletionSource<string>().Task, new TaskCompletionSource<string>().Task);
        _handle.HasExited.Returns(false);
        using var cts = new CancellationTokenSource();

        var run = CreateRunner(TimeSpan.FromMinutes(5)).RunAsync(Request(FullyQualifiedPath), cts.Token);
        await cts.CancelAsync();

        await FluentActions.Awaiting(() => run).Should().ThrowAsync<OperationCanceledException>();
        _handle.Received(1).KillEntireProcessTree();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reads_that_fault_after_the_runner_gave_up_never_raise_UnobservedTaskException(bool viaCancellation)
    {
        var marker = new ObjectDisposedException($"car-unobserved-marker-{viaCancellation}");
        var raised = false;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            if (e.Exception.InnerExceptions.Contains(marker))
            {
                raised = true;
            }
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await RunThenFaultReadsAsync(marker, viaCancellation);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }

        raised.Should().BeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task RunThenFaultReadsAsync(Exception marker, bool viaCancellation)
    {
        var stdout = new TaskCompletionSource<string>();
        var stderr = new TaskCompletionSource<string>();
        var handle = Substitute.For<IProcessHandle>();
        handle.ReadStandardOutputToEndAsync(Arg.Any<CancellationToken>()).Returns(_ => stdout.Task);
        handle.ReadStandardErrorToEndAsync(Arg.Any<CancellationToken>()).Returns(_ => stderr.Task);
        handle.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            viaCancellation ? Task.FromException(new OperationCanceledException()) : Task.CompletedTask);
        handle.HasExited.Returns(true);
        var starter = Substitute.For<IProcessStarter>();
        starter.Start(Arg.Any<ProcessStartInfo>()).Returns(handle);

        var run = new ProcessRunner(Substitute.For<IExecutableResolver>(), starter, TinyDrainTimeout)
            .RunAsync(Request(FullyQualifiedPath));
        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
            // Expected on the cancellation path.
        }

        stdout.SetException(marker);
        stderr.SetException(marker);
    }
}
