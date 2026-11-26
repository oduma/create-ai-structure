using CreateAiStructure.Domain.Tooling;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Tooling;

public class RequiredToolsTests
{
    [Fact]
    public void All_EqualsExpectedToolsInOrder()
    {
        RequiredTools.All.Should().Equal("git", "gh", "depviz", "t3");
    }

    [Fact]
    public void All_CannotBeMutatedThroughCast()
    {
        var mutable = (IList<string>)RequiredTools.All;

        FluentActions.Invoking(() => mutable.Add("x")).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => mutable[0] = "x").Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => mutable.Clear()).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => mutable.RemoveAt(0)).Should().Throw<NotSupportedException>();
    }
}
