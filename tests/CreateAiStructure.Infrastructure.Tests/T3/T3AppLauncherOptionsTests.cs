using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.T3;

public class T3AppLauncherOptionsTests
{
    private static readonly TimeSpan ValidDelay = TimeSpan.FromSeconds(1);

    public static TheoryData<TimeSpan> NonPositiveDurations => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromTicks(-1),
        TimeSpan.FromSeconds(-1),
        TimeSpan.MinValue,
    };

    [Fact]
    public void Default_exposes_two_second_delay_one_second_interval_and_ten_attempts()
    {
        var options = T3AppLauncherOptions.Default;

        options.InitialStartupDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.RetryInterval.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxRetryAttempts.Should().Be(10);
    }

    [Fact]
    public void Default_returns_the_same_instance_on_repeated_access()
    {
        var first = T3AppLauncherOptions.Default;
        var second = T3AppLauncherOptions.Default;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Constructor_accepts_minimum_boundary_values()
    {
        var options = new T3AppLauncherOptions(TimeSpan.FromTicks(1), TimeSpan.FromTicks(1), 1);

        options.InitialStartupDelay.Should().Be(TimeSpan.FromTicks(1));
        options.RetryInterval.Should().Be(TimeSpan.FromTicks(1));
        options.MaxRetryAttempts.Should().Be(1);
    }

    [Fact]
    public void Constructor_exposes_supplied_values()
    {
        var options = new T3AppLauncherOptions(
            TimeSpan.FromMilliseconds(1500),
            TimeSpan.FromMilliseconds(250),
            42);

        options.InitialStartupDelay.Should().Be(TimeSpan.FromMilliseconds(1500));
        options.RetryInterval.Should().Be(TimeSpan.FromMilliseconds(250));
        options.MaxRetryAttempts.Should().Be(42);
    }

    [Theory]
    [MemberData(nameof(NonPositiveDurations))]
    public void Constructor_rejects_non_positive_initial_startup_delay(TimeSpan initialStartupDelay)
    {
        var act = () => new T3AppLauncherOptions(initialStartupDelay, ValidDelay, 1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("initialStartupDelay");
    }

    [Theory]
    [MemberData(nameof(NonPositiveDurations))]
    public void Constructor_rejects_non_positive_retry_interval(TimeSpan retryInterval)
    {
        var act = () => new T3AppLauncherOptions(ValidDelay, retryInterval, 1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("retryInterval");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_rejects_max_retry_attempts_below_one(int maxRetryAttempts)
    {
        var act = () => new T3AppLauncherOptions(ValidDelay, ValidDelay, maxRetryAttempts);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maxRetryAttempts");
    }
}
