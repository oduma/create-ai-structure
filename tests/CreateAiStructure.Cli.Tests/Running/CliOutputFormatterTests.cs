using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Domain.Common;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Running;

public class CliOutputFormatterTests
{
    [Fact]
    public void FormatVersion_prefixes_the_tool_name()
    {
        CliOutputFormatter.FormatVersion("1.2.3").Should().Be("create-ai-structure 1.2.3");
    }

    [Fact]
    public void FormatError_renders_code_and_message()
    {
        CliOutputFormatter.FormatError(new DomainError("Some.Code", "Something failed."))
            .Should().Be("Error [Some.Code]: Something failed.");
    }

    [Fact]
    public void FormatError_throws_when_error_is_null()
    {
        var act = () => CliOutputFormatter.FormatError(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("error");
    }

    [Theory]
    [InlineData(true, "Started the T3 Code desktop app.")]
    [InlineData(false, "T3 Code desktop app was already running.")]
    public void FormatSuccess_returns_the_summary_lines_in_order(bool startedT3, string t3Status)
    {
        var result = new ScaffoldResult("/repos/demo", ["a", "b"], startedT3, "Reminder text.");

        CliOutputFormatter.FormatSuccess(result).Should().Equal(
            "Repository created at /repos/demo",
            "Materialized 2 files.",
            t3Status,
            "Reminder text.");
    }

    [Fact]
    public void FormatSuccess_throws_when_result_is_null()
    {
        var act = () => CliOutputFormatter.FormatSuccess(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("result");
    }

    [Fact]
    public void Message_constants_match_the_specification()
    {
        CliOutputFormatter.CancelledMessage.Should().Be("Operation cancelled.");
        CliOutputFormatter.T3StartedMessage.Should().Be("Started the T3 Code desktop app.");
        CliOutputFormatter.T3AlreadyRunningMessage.Should().Be("T3 Code desktop app was already running.");
    }
}
