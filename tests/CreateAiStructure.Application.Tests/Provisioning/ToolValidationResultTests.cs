using CreateAiStructure.Application.Provisioning;
using FluentAssertions;

namespace CreateAiStructure.Application.Tests.Provisioning;

public class ToolValidationResultTests
{
    [Fact]
    public void Constructor_rejects_null_found_tools()
    {
        var act = () => new ToolValidationResult(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_rejects_null_missing_tools()
    {
        var act = () => new ToolValidationResult(["git"], null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_exposes_values()
    {
        IReadOnlyList<string> found = ["git", "gh"];
        IReadOnlyList<string> missing = ["t3"];

        var result = new ToolValidationResult(found, missing);

        result.FoundTools.Should().BeSameAs(found);
        result.MissingTools.Should().BeSameAs(missing);
    }

    [Fact]
    public void Is_valid_when_no_tools_are_missing()
    {
        var result = new ToolValidationResult(["git", "gh", "depviz", "t3"], []);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Is_not_valid_when_a_tool_is_missing()
    {
        var result = new ToolValidationResult(["git"], ["gh"]);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Results_with_the_same_values_are_equal()
    {
        IReadOnlyList<string> found = ["git"];
        IReadOnlyList<string> missing = ["gh"];

        var first = new ToolValidationResult(found, missing);
        var second = new ToolValidationResult(found, missing);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
