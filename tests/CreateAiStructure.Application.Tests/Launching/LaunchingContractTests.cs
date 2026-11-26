using CreateAiStructure.Application.Launching;
using CreateAiStructure.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Application.Tests.Launching;

public class LaunchingContractTests
{
    [Fact]
    public async Task T3_app_launcher_is_mockable()
    {
        var launcher = Substitute.For<IT3AppLauncher>();
        var expected = Result<T3LaunchOutcome>.Success(new T3LaunchOutcome(true, "reminder"));
        using var cancellation = new CancellationTokenSource();
        launcher.LaunchAsync(@"C:\work\repo", cancellation.Token).Returns(Task.FromResult(expected));
        launcher.LaunchAsync(@"C:\other\repo").Returns(Task.FromResult(expected));

        var withToken = await launcher.LaunchAsync(@"C:\work\repo", cancellation.Token);
        var withDefault = await launcher.LaunchAsync(@"C:\other\repo");

        withToken.Should().BeSameAs(expected);
        withDefault.Should().BeSameAs(expected);
        _ = launcher.Received(1).LaunchAsync(@"C:\work\repo", cancellation.Token);
        _ = launcher.Received(1).LaunchAsync(@"C:\other\repo");
    }
}
