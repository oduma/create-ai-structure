using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Running;

public class CliExitCodesTests
{
    [Fact]
    public void Exit_codes_match_the_specification()
    {
        CliExitCodes.Success.Should().Be(0);
        CliExitCodes.ExecutionFailure.Should().Be(1);
        CliExitCodes.UsageError.Should().Be(2);
        CliExitCodes.Cancelled.Should().Be(130);
    }
}
