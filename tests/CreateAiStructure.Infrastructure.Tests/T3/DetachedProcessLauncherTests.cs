using System.ComponentModel;
using System.Diagnostics;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Infrastructure.Tests.T3;

public class DetachedProcessLauncherTests
{
    private const string ExecutablePath = @"C:\Program Files\T3 Code\T3 Code.exe";
    private const string WorkingDirectoryPath = @"C:\Users\u\work";
    private const string InvalidPathCode = "T3Launch.InvalidExecutablePath";
    private const string StartFailedCode = "T3Launch.DesktopStartFailed";
    private const string ExceptionMessage = "simulated start failure";

    private readonly IDisposable _processHandle = Substitute.For<IDisposable>();
    private ProcessStartInfo? _capturedStartInfo;
    private int _starterCalls;

    public static TheoryData<string> UnsafePaths => new()
    {
        "t3.exe",
        @".\t3.exe",
        @"C:\a\..\b.exe",
        "C:/a/../b.exe",
    };

    public static TheoryData<string?> BlankWorkingDirectories => new()
    {
        null,
        "",
        "   ",
    };

    public static TheoryData<Exception> CaughtExceptions => new()
    {
        new Win32Exception(2, ExceptionMessage),
        new FileNotFoundException(ExceptionMessage),
        new InvalidOperationException(ExceptionMessage),
        new UnauthorizedAccessException(ExceptionMessage),
    };

    [Fact]
    public void Constructor_WhenProcessStarterIsNull_ThrowsArgumentNullException()
    {
        var act = () => new DetachedProcessLauncher(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("processStarter");
    }

    [Fact]
    public void StartDetached_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var launcher = CreateLauncher();

        var act = () => launcher.StartDetached(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Theory]
    [MemberData(nameof(UnsafePaths))]
    public void StartDetached_WhenFileNameIsNotSafeAbsolutePath_ReturnsInvalidExecutablePathWithoutStarting(string path)
    {
        var launcher = CreateLauncher();

        var result = launcher.StartDetached(new ProcessExecutionRequest(path, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(InvalidPathCode);
        result.Error.Message.Should().Contain(path);
        _starterCalls.Should().Be(0);
    }

    [Fact]
    public void StartDetached_WhenStarted_PassesFileNameWithoutShellOrRedirection()
    {
        var launcher = CreateLauncher();

        launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, []));

        _starterCalls.Should().Be(1);
        _capturedStartInfo.Should().NotBeNull();
        _capturedStartInfo!.FileName.Should().Be(ExecutablePath);
        _capturedStartInfo.UseShellExecute.Should().BeFalse();
        _capturedStartInfo.RedirectStandardInput.Should().BeFalse();
        _capturedStartInfo.RedirectStandardOutput.Should().BeFalse();
        _capturedStartInfo.RedirectStandardError.Should().BeFalse();
    }

    [Fact]
    public void StartDetached_WhenArgumentsProvided_CopiesThemVerbatimIntoArgumentList()
    {
        string[] arguments = ["--flag", "a b", "--x=\"q\""];
        var launcher = CreateLauncher();

        launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, arguments));

        _capturedStartInfo!.ArgumentList.Should().Equal(arguments);
        _capturedStartInfo.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void StartDetached_WhenNoArguments_LeavesArgumentListAndArgumentsEmpty()
    {
        var launcher = CreateLauncher();

        launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, []));

        _capturedStartInfo!.ArgumentList.Should().BeEmpty();
        _capturedStartInfo.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void StartDetached_WhenWorkingDirectoryProvided_SetsWorkingDirectory()
    {
        var launcher = CreateLauncher();

        launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, [], WorkingDirectoryPath));

        _capturedStartInfo!.WorkingDirectory.Should().Be(WorkingDirectoryPath);
    }

    [Theory]
    [MemberData(nameof(BlankWorkingDirectories))]
    public void StartDetached_WhenWorkingDirectoryIsNullOrWhiteSpace_LeavesWorkingDirectoryEmpty(string? workingDirectory)
    {
        var launcher = CreateLauncher();

        launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, [], workingDirectory));

        _capturedStartInfo!.WorkingDirectory.Should().BeEmpty();
    }

    [Fact]
    public void StartDetached_WhenProcessStarts_ReturnsSuccessAndDisposesHandleOnce()
    {
        var launcher = CreateLauncher();

        var result = launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, ["--flag"]));

        result.IsSuccess.Should().BeTrue();
        _processHandle.Received(1).Dispose();
    }

    [Fact]
    public void StartDetached_WhenStarterReturnsNull_ReturnsDesktopStartFailed()
    {
        var launcher = new DetachedProcessLauncher(_ => null);

        var result = launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(StartFailedCode);
        result.Error.Message.Should().Contain(ExecutablePath);
    }

    [Theory]
    [MemberData(nameof(CaughtExceptions))]
    public void StartDetached_WhenStarterThrowsExpectedException_ReturnsDesktopStartFailed(Exception exception)
    {
        var launcher = new DetachedProcessLauncher(_ => throw exception);

        var result = launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, []));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(StartFailedCode);
        result.Error.Message.Should().Contain(ExecutablePath);
        result.Error.Message.Should().Contain(exception.Message);
    }

    [Fact]
    public void StartDetached_WhenStarterThrowsUnexpectedException_Propagates()
    {
        var launcher = new DetachedProcessLauncher(_ => throw new ArgumentException(ExceptionMessage));

        var act = () => launcher.StartDetached(new ProcessExecutionRequest(ExecutablePath, []));

        act.Should().Throw<ArgumentException>().WithMessage(ExceptionMessage);
    }

    private DetachedProcessLauncher CreateLauncher() =>
        new(startInfo =>
        {
            _starterCalls++;
            _capturedStartInfo = startInfo;
            return _processHandle;
        });
}
