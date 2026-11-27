using CreateAiStructure.Cli.ConsoleIo;
using CreateAiStructure.Cli.Parsing;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Running;

public class CliHostTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private ConsoleStreams Streams => new(_out, _error);

    [Fact]
    public async Task Help_returns_success_and_writes_usage_to_stdout()
    {
        var exitCode = await CliHost.RunAsync(["--help"], Streams);

        exitCode.Should().Be(CliExitCodes.Success);
        _out.ToString().Should().Be(CliUsage.Text + Environment.NewLine);
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_args_return_usage_error_and_report_missing_required_options()
    {
        var exitCode = await CliHost.RunAsync([], Streams);

        exitCode.Should().Be(CliExitCodes.UsageError);
        _error.ToString().Should().Contain(CliErrorCodes.MissingRequiredOptions);
        _out.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_throws_when_args_is_null()
    {
        var act = () => CliHost.RunAsync(null!, Streams);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("args");
    }

    [Fact]
    public async Task RunAsync_throws_when_streams_is_null()
    {
        var act = () => CliHost.RunAsync([], null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("streams");
    }
}
