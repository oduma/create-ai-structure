using CreateAiStructure.Application.Runtime;
using FluentAssertions;

namespace CreateAiStructure.Application.Tests.Runtime;

public class ProcessExecutionRequestTests
{
    public static TheoryData<string?> BlankValues => new()
    {
        null,
        "",
        " ",
        "\t",
        "\r\n",
    };

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void Constructor_rejects_blank_file_name(string? fileName)
    {
        var act = () => new ProcessExecutionRequest(fileName!, ["--version"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_rejects_null_arguments()
    {
        var act = () => new ProcessExecutionRequest("git", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_exposes_values()
    {
        IReadOnlyList<string> arguments = ["status", "--short"];

        var request = new ProcessExecutionRequest("git", arguments, @"C:\work");

        request.FileName.Should().Be("git");
        request.Arguments.Should().BeSameAs(arguments);
        request.WorkingDirectory.Should().Be(@"C:\work");
    }

    [Fact]
    public void Working_directory_defaults_to_null()
    {
        var request = new ProcessExecutionRequest("git", ["status"]);

        request.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public void Requests_with_the_same_values_are_equal()
    {
        IReadOnlyList<string> arguments = ["status"];

        var first = new ProcessExecutionRequest("git", arguments, @"C:\work");
        var second = new ProcessExecutionRequest("git", arguments, @"C:\work");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
