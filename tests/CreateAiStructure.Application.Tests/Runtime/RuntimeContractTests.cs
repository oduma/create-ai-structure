using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Application.Tests.Runtime;

public class RuntimeContractTests
{
    private static ProcessExecutionRequest Request() => new("git", ["--version"]);

    [Fact]
    public async Task Process_runner_is_mockable()
    {
        var runner = Substitute.For<IProcessRunner>();
        var request = Request();
        var expected = new ProcessExecutionResult(0, "git version 2.50", "");
        using var cancellation = new CancellationTokenSource();
        runner.RunAsync(request, cancellation.Token).Returns(Task.FromResult(expected));
        runner.RunAsync(request).Returns(Task.FromResult(expected));

        var withToken = await runner.RunAsync(request, cancellation.Token);
        var withDefault = await runner.RunAsync(request);

        withToken.Should().BeSameAs(expected);
        withDefault.Should().BeSameAs(expected);
        _ = runner.Received(1).RunAsync(request, cancellation.Token);
        _ = runner.Received(1).RunAsync(request);
    }

    [Fact]
    public void Detached_process_launcher_is_mockable()
    {
        var launcher = Substitute.For<IDetachedProcessLauncher>();
        var request = Request();
        var expected = Result.Success();
        launcher.StartDetached(request).Returns(expected);

        var actual = launcher.StartDetached(request);

        actual.Should().BeSameAs(expected);
        launcher.Received(1).StartDetached(request);
    }

    [Fact]
    public async Task File_system_is_mockable()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        IReadOnlyList<string> files = [@"C:\t3\a.exe"];
        fileSystem.DirectoryExists(@"C:\work").Returns(true);
        fileSystem.FileExists(@"C:\work\file.txt").Returns(true);
        fileSystem.GetFiles(@"C:\t3", "*.exe").Returns(files);
        using var cancellation = new CancellationTokenSource();

        fileSystem.DirectoryExists(@"C:\work").Should().BeTrue();
        fileSystem.FileExists(@"C:\work\file.txt").Should().BeTrue();
        fileSystem.GetFiles(@"C:\t3", "*.exe").Should().BeSameAs(files);
        fileSystem.CreateDirectory(@"C:\work\new");
        await fileSystem.WriteAllTextUtf8NoBomAsync(@"C:\work\a.md", "content", cancellation.Token);
        await fileSystem.WriteAllTextUtf8NoBomAsync(@"C:\work\b.md", "content");

        fileSystem.Received(1).DirectoryExists(@"C:\work");
        fileSystem.Received(1).FileExists(@"C:\work\file.txt");
        fileSystem.Received(1).GetFiles(@"C:\t3", "*.exe");
        fileSystem.Received(1).CreateDirectory(@"C:\work\new");
        _ = fileSystem.Received(1).WriteAllTextUtf8NoBomAsync(@"C:\work\a.md", "content", cancellation.Token);
        _ = fileSystem.Received(1).WriteAllTextUtf8NoBomAsync(@"C:\work\b.md", "content");
    }

    [Fact]
    public void Environment_provider_is_mockable()
    {
        var environment = Substitute.For<IEnvironmentProvider>();
        environment.GetEnvironmentVariable("PATH").Returns(@"C:\bin");
        environment.GetEnvironmentVariable("MISSING").Returns((string?)null);
        environment.GetLocalApplicationDataPath().Returns(@"C:\Users\me\AppData\Local");
        environment.GetCurrentDirectory().Returns(@"C:\work");

        environment.GetEnvironmentVariable("PATH").Should().Be(@"C:\bin");
        environment.GetEnvironmentVariable("MISSING").Should().BeNull();
        environment.GetLocalApplicationDataPath().Should().Be(@"C:\Users\me\AppData\Local");
        environment.GetCurrentDirectory().Should().Be(@"C:\work");

        environment.Received(1).GetEnvironmentVariable("PATH");
        environment.Received(1).GetEnvironmentVariable("MISSING");
        environment.Received(1).GetLocalApplicationDataPath();
        environment.Received(1).GetCurrentDirectory();
    }

    [Fact]
    public void Clock_is_mockable()
    {
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);

        var actual = clock.UtcNow;

        actual.Should().Be(now);
        _ = clock.Received(1).UtcNow;
    }

    [Fact]
    public async Task Delay_provider_is_mockable()
    {
        var delayProvider = Substitute.For<IDelayProvider>();
        var delay = TimeSpan.FromSeconds(3);
        using var cancellation = new CancellationTokenSource();
        delayProvider.DelayAsync(delay, cancellation.Token).Returns(Task.CompletedTask);

        await delayProvider.DelayAsync(delay, cancellation.Token);
        await delayProvider.DelayAsync(delay);

        _ = delayProvider.Received(1).DelayAsync(delay, cancellation.Token);
        _ = delayProvider.Received(1).DelayAsync(delay);
    }
}
