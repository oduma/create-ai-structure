using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.Provisioning;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Provisioning;

public class SubprocessDiagnosticsTests
{
    private const string Fallback = "No diagnostic output was captured.";

    [Fact]
    public void Prefers_trimmed_standard_error_when_it_has_content()
    {
        var result = new ProcessExecutionResult(1, "  some output  ", "  \tfatal: boom \r\n");

        var description = SubprocessDiagnostics.Describe("gh repo create", result);

        description.Should().Be("gh repo create failed with exit code 1: fatal: boom");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \r\n\t ")]
    public void Falls_back_to_trimmed_standard_output_when_standard_error_is_blank(string standardError)
    {
        var result = new ProcessExecutionResult(128, "\n  remote said no  \n", standardError);

        var description = SubprocessDiagnostics.Describe("git branch -M main", result);

        description.Should().Be("git branch -M main failed with exit code 128: remote said no");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("  ", " \n")]
    [InlineData("\r\n", "\t")]
    public void Uses_fallback_detail_when_both_streams_are_blank(string standardOutput, string standardError)
    {
        var result = new ProcessExecutionResult(2, standardOutput, standardError);

        var description = SubprocessDiagnostics.Describe("git remote get-url origin", result);

        description.Should().Be($"git remote get-url origin failed with exit code 2: {Fallback}");
    }

    [Fact]
    public void Includes_negative_exit_codes_verbatim()
    {
        var result = new ProcessExecutionResult(-1, string.Empty, "killed");

        var description = SubprocessDiagnostics.Describe("git commit", result);

        description.Should().Be("git commit failed with exit code -1: killed");
    }
}
