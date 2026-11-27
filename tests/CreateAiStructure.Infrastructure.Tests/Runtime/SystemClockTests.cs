using CreateAiStructure.Infrastructure.Runtime;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Runtime;

/// <summary>
/// Specification of <see cref="SystemClock"/> (Sub-Issue #35, AC-1).
/// </summary>
public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_HasZeroOffset_AndLiesBetweenSamples()
    {
        var sut = new SystemClock();

        var before = DateTimeOffset.UtcNow;
        var now = sut.UtcNow;
        var after = DateTimeOffset.UtcNow;

        now.Offset.Should().Be(TimeSpan.Zero);
        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
