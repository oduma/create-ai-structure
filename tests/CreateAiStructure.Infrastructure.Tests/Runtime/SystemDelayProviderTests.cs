using CreateAiStructure.Infrastructure.Runtime;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Runtime;

/// <summary>
/// Specification of <see cref="SystemDelayProvider"/> (Sub-Issue #35, AC-1).
/// </summary>
public sealed class SystemDelayProviderTests
{
    private readonly SystemDelayProvider _sut = new();

    [Fact]
    public async Task DelayAsync_Completes_ForZeroDelay()
    {
        var act = () => _sut.DelayAsync(TimeSpan.Zero);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DelayAsync_Completes_ForSmallDelay()
    {
        var act = () => _sut.DelayAsync(TimeSpan.FromMilliseconds(10));

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void DelayAsync_Throws_ForNegativeDelay()
    {
        Action act = () => _sut.DelayAsync(TimeSpan.FromMilliseconds(-1));

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("delay");
    }

    [Fact]
    public async Task DelayAsync_Throws_ForPreCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _sut.DelayAsync(TimeSpan.FromSeconds(30), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
