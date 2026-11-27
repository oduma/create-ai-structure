using CreateAiStructure.Cli.ConsoleIo;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Running;

public class ConsoleStreamsTests
{
    [Fact]
    public void Constructor_exposes_the_supplied_writers()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var streams = new ConsoleStreams(output, error);

        streams.Out.Should().BeSameAs(output);
        streams.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Constructor_throws_when_out_is_null()
    {
        var act = () => new ConsoleStreams(null!, new StringWriter());

        // ThrowIfNull captures the argument expression, which is the verbatim identifier "@out".
        act.Should().Throw<ArgumentNullException>().WithParameterName("@out");
    }

    [Fact]
    public void Constructor_throws_when_error_is_null()
    {
        var act = () => new ConsoleStreams(new StringWriter(), null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("error");
    }
}
