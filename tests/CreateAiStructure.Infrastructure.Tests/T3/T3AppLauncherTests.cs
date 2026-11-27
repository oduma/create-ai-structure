using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;
using NSubstitute;
using static CreateAiStructure.Infrastructure.Tests.T3.T3AppLauncherFixture;

namespace CreateAiStructure.Infrastructure.Tests.T3;

/// <summary>Constructor guards, defaults and <c>LaunchAsync</c> pre-flight validation.</summary>
public class T3AppLauncherTests
{
    private readonly T3AppLauncherFixture _fixture = new();

    public static TheoryData<string?> BlankDirectories => new()
    {
        null,
        "",
        "   ",
        "\t\n",
    };

    [Fact]
    public void T3AppLauncher_ImplementsIT3AppLauncher()
    {
        _fixture.CreateSut().Should().BeAssignableTo<IT3AppLauncher>();
    }

    [Fact]
    public void ImportReminderMessage_IsTheDeterministicReminderText()
    {
        T3AppLauncher.ImportReminderMessage.Should()
            .Be("Reminder: Import t3.json in T3 Code to enable workspace scripts.");
    }

    [Fact]
    public void ImportReminderMessage_MentionsT3JsonAndT3Code()
    {
        T3AppLauncher.ImportReminderMessage.Should().Contain("t3.json").And.Contain("T3 Code");
    }

    [Fact]
    public void Constructor_WhenProcessRunnerIsNull_ThrowsArgumentNullException()
    {
        var act = () => new T3AppLauncher(
            null!, _fixture.DetachedProcessLauncher, _fixture.FileSystem, _fixture.Locator, _fixture.DelayProvider);

        act.Should().Throw<ArgumentNullException>().WithParameterName("processRunner");
    }

    [Fact]
    public void Constructor_WhenDetachedProcessLauncherIsNull_ThrowsArgumentNullException()
    {
        var act = () => new T3AppLauncher(
            _fixture.ProcessRunner, null!, _fixture.FileSystem, _fixture.Locator, _fixture.DelayProvider);

        act.Should().Throw<ArgumentNullException>().WithParameterName("detachedProcessLauncher");
    }

    [Fact]
    public void Constructor_WhenFileSystemIsNull_ThrowsArgumentNullException()
    {
        var act = () => new T3AppLauncher(
            _fixture.ProcessRunner, _fixture.DetachedProcessLauncher, null!, _fixture.Locator, _fixture.DelayProvider);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    [Fact]
    public void Constructor_WhenDesktopExecutableLocatorIsNull_ThrowsArgumentNullException()
    {
        var act = () => new T3AppLauncher(
            _fixture.ProcessRunner, _fixture.DetachedProcessLauncher, _fixture.FileSystem, null!, _fixture.DelayProvider);

        act.Should().Throw<ArgumentNullException>().WithParameterName("desktopExecutableLocator");
    }

    [Fact]
    public void Constructor_WhenDelayProviderIsNull_ThrowsArgumentNullException()
    {
        var act = () => new T3AppLauncher(
            _fixture.ProcessRunner, _fixture.DetachedProcessLauncher, _fixture.FileSystem, _fixture.Locator, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("delayProvider");
    }

    [Fact]
    public async Task Constructor_WhenOptionsIsNull_UsesDefaultInitialStartupDelay()
    {
        _fixture.SetupRunResults(PipeFailure(), Success());

        var result = await _fixture.CreateSut(options: null).LaunchAsync(RepoDir);

        result.IsSuccess.Should().BeTrue();
        _fixture.Delays.Should().Equal(T3AppLauncherOptions.Default.InitialStartupDelay);
        _fixture.Delays.Should().Equal(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Constructor_WhenOptionsIsOmitted_UsesDefaultInitialStartupDelay()
    {
        _fixture.SetupRunResults(PipeFailure(), Success());

        var result = await _fixture.CreateSutWithoutOptions().LaunchAsync(RepoDir);

        result.IsSuccess.Should().BeTrue();
        _fixture.Delays.Should().Equal(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Constructor_WhenOptionsIsNull_ExhaustsDefaultTenRetriesWithDefaultIntervals()
    {
        _fixture.SetupRunResults(PipeFailure());

        var result = await _fixture.CreateSut(options: null).LaunchAsync(RepoDir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReadinessTimeoutCode);
        TimeSpan[] expectedDelays = [TimeSpan.FromSeconds(2), .. Enumerable.Repeat(TimeSpan.FromSeconds(1), 9)];
        _fixture.Delays.Should().Equal(expectedDelays);
        _fixture.RunRequests.Should().HaveCount(11);
    }

    [Theory]
    [MemberData(nameof(BlankDirectories))]
    public async Task LaunchAsync_WhenRepositoryDirectoryIsBlank_ThrowsArgumentExceptionWithoutAnyCalls(string? directory)
    {
        var sut = _fixture.CreateSut();

        var act = () => sut.LaunchAsync(directory!);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("repositoryDirectory");
        AssertNoCallsWereMade();
    }

    [Fact]
    public async Task LaunchAsync_WhenTokenIsAlreadyCancelled_ThrowsOperationCanceledExceptionBeforeAnyCall()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var sut = _fixture.CreateSut();

        var act = () => sut.LaunchAsync(RepoDir, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        AssertNoCallsWereMade();
    }

    [Fact]
    public async Task LaunchAsync_WhenRepositoryDirectoryDoesNotExist_ReturnsRepositoryDirectoryNotFound()
    {
        const string missingDirectory = @"C:\repos\missing-repo";
        _fixture.FileSystem.DirectoryExists(missingDirectory).Returns(false);

        var result = await _fixture.CreateSut().LaunchAsync(missingDirectory);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(RepositoryDirectoryNotFoundCode);
        result.Error.Message.Should().Contain(missingDirectory);
        _fixture.FileSystem.Received(1).DirectoryExists(missingDirectory);
    }

    [Fact]
    public async Task LaunchAsync_WhenRepositoryDirectoryDoesNotExist_MakesNoDownstreamCalls()
    {
        const string missingDirectory = @"C:\repos\missing-repo";
        _fixture.FileSystem.DirectoryExists(missingDirectory).Returns(false);

        await _fixture.CreateSut().LaunchAsync(missingDirectory);

        _ = _fixture.ProcessRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
        _fixture.Locator.DidNotReceiveWithAnyArgs().Locate();
        _fixture.DetachedProcessLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }

    private void AssertNoCallsWereMade()
    {
        _fixture.FileSystem.DidNotReceiveWithAnyArgs().DirectoryExists(default!);
        _ = _fixture.ProcessRunner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
        _fixture.Locator.DidNotReceiveWithAnyArgs().Locate();
        _fixture.DetachedProcessLauncher.DidNotReceiveWithAnyArgs().StartDetached(default!);
        _ = _fixture.DelayProvider.DidNotReceiveWithAnyArgs().DelayAsync(default, default);
    }
}
