using CreateAiStructure.Application.Launching;
using FluentAssertions;

namespace CreateAiStructure.Application.Tests.Launching;

public class T3LaunchOutcomeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_exposes_values(bool startedDesktopApp)
    {
        var outcome = new T3LaunchOutcome(startedDesktopApp, "Import the repository in T3 Code.");

        outcome.StartedDesktopApp.Should().Be(startedDesktopApp);
        outcome.ReminderMessage.Should().Be("Import the repository in T3 Code.");
    }

    [Fact]
    public void Outcomes_with_the_same_values_are_equal()
    {
        var first = new T3LaunchOutcome(true, "reminder");
        var second = new T3LaunchOutcome(true, "reminder");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
