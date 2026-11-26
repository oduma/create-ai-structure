using CreateAiStructure.Application.Runtime;
using FluentAssertions;

namespace CreateAiStructure.Application.Tests.Runtime;

public class ProcessExecutionResultTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    public void Succeeded_is_true_only_for_exit_code_zero(int exitCode, bool expected)
    {
        var result = new ProcessExecutionResult(exitCode, "out", "err");

        result.Succeeded.Should().Be(expected);
    }

    [Fact]
    public void Constructor_exposes_values()
    {
        var result = new ProcessExecutionResult(2, "standard output", "standard error");

        result.ExitCode.Should().Be(2);
        result.StandardOutput.Should().Be("standard output");
        result.StandardError.Should().Be("standard error");
    }

    [Fact]
    public void Results_with_the_same_values_are_equal()
    {
        var first = new ProcessExecutionResult(0, "out", "err");
        var second = new ProcessExecutionResult(0, "out", "err");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
