using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.T3;

public class T3PipeErrorClassifierTests
{
    private const int FailureExitCode = 1;

    private static readonly string[] Indicators =
    [
        @"\\.\pipe\",
        "named pipe",
        "pipe unreachable",
        "pipe is unreachable",
        "connect enoent",
        "econnrefused",
        "not running",
        "isn't running",
        "failed to connect",
        "could not connect",
        "cannot connect",
        "unable to connect",
        "connection refused",
    ];

    public static TheoryData<string> AllIndicators
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var indicator in Indicators)
            {
                data.Add(indicator);
            }

            return data;
        }
    }

    public static TheoryData<string> RealisticPipeFailureMessages => new()
    {
        @"Error: connect ENOENT \\.\pipe\t3code",
        "Error: connect ECONNREFUSED 127.0.0.1:4500",
        "T3 Code isn't running. Start the desktop app and try again.",
        "Could Not Connect to the T3 Code desktop app.",
        "Named Pipe Unreachable",
    };

    public static TheoryData<string> UnrelatedFailureMessages => new()
    {
        "Executable 't3' was not found on PATH.",
        "DesktopAppSshUnsupportedError: The T3 Code desktop app is local-only. It cannot run over SSH.",
        "error: unknown option '--foo'",
        "EACCES: permission denied",
    };

    public static TheoryData<string> BlankValues => new()
    {
        string.Empty,
        " ",
        "   ",
        "\t",
        "\r\n",
        " \t \n ",
    };

    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void IsPipeUnreachable_returns_true_when_indicator_is_in_standard_error_only(string indicator)
    {
        var result = Failed(standardOutput: string.Empty, standardError: $"t3: {indicator} (details)");

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void IsPipeUnreachable_returns_true_when_indicator_is_in_standard_output_only(string indicator)
    {
        var result = Failed(standardOutput: $"t3: {indicator} (details)", standardError: string.Empty);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void IsPipeUnreachable_returns_true_when_upper_case_indicator_is_in_standard_error(string indicator)
    {
        var result = Failed(standardOutput: string.Empty, standardError: indicator.ToUpperInvariant());

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void IsPipeUnreachable_returns_true_when_upper_case_indicator_is_in_standard_output(string indicator)
    {
        var result = Failed(standardOutput: indicator.ToUpperInvariant(), standardError: string.Empty);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(RealisticPipeFailureMessages))]
    public void IsPipeUnreachable_returns_true_for_realistic_mixed_case_pipe_failures(string message)
    {
        var result = Failed(standardOutput: string.Empty, standardError: message);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void IsPipeUnreachable_returns_false_when_result_succeeded_even_if_output_contains_indicator(string indicator)
    {
        var result = new ProcessExecutionResult(0, indicator, indicator);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void IsPipeUnreachable_returns_false_when_both_streams_are_blank(string blank)
    {
        var result = Failed(standardOutput: blank, standardError: blank);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void IsPipeUnreachable_returns_true_when_standard_output_is_blank_and_standard_error_matches(string blank)
    {
        var result = Failed(standardOutput: blank, standardError: @"connect ENOENT \\.\pipe\t3code");

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void IsPipeUnreachable_returns_true_when_standard_error_is_blank_and_standard_output_matches(string blank)
    {
        var result = Failed(standardOutput: "T3 Code isn't running", standardError: blank);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Fact]
    public void IsPipeUnreachable_returns_false_when_both_streams_are_null()
    {
        var result = Failed(standardOutput: null!, standardError: null!);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeFalse();
    }

    [Fact]
    public void IsPipeUnreachable_returns_true_when_standard_output_is_null_and_standard_error_matches()
    {
        var result = Failed(standardOutput: null!, standardError: "ECONNREFUSED");

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Fact]
    public void IsPipeUnreachable_returns_true_when_standard_error_is_null_and_standard_output_matches()
    {
        var result = Failed(standardOutput: "connection refused", standardError: null!);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(UnrelatedFailureMessages))]
    public void IsPipeUnreachable_returns_false_for_unrelated_failure_in_standard_error(string message)
    {
        var result = Failed(standardOutput: string.Empty, standardError: message);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(UnrelatedFailureMessages))]
    public void IsPipeUnreachable_returns_false_for_unrelated_failure_in_standard_output(string message)
    {
        var result = Failed(standardOutput: message, standardError: string.Empty);

        T3PipeErrorClassifier.IsPipeUnreachable(result).Should().BeFalse();
    }

    [Fact]
    public void IsPipeUnreachable_rejects_null_result()
    {
        var act = () => T3PipeErrorClassifier.IsPipeUnreachable(null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("result");
    }

    private static ProcessExecutionResult Failed(string standardOutput, string standardError) =>
        new(FailureExitCode, standardOutput, standardError);
}
