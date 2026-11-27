using CreateAiStructure.Cli.Parsing;
using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Parsing;

public class CliUsageTests
{
    [Fact]
    public void Text_starts_with_the_synopsis()
    {
        CliUsage.Text.Should().StartWith(
            "Usage: create-ai-structure --type <webApp|api|console|MAUI> --repo <repository-name>");
    }

    [Theory]
    [InlineData("--type")]
    [InlineData("--repo")]
    [InlineData("-h|--help")]
    [InlineData("-v|--version")]
    [InlineData("--type=")]
    [InlineData("--repo=")]
    public void Text_lists_every_flag(string flag)
    {
        CliUsage.Text.Should().Contain(flag);
    }

    [Fact]
    public void Text_lists_every_supported_project_type()
    {
        CliUsage.Text.Should().ContainAll(ProjectType.All.Select(type => type.CanonicalName));
        CliUsage.Text.Should().Contain("webApp, api, console, MAUI");
    }

    [Fact]
    public void Text_includes_an_example()
    {
        CliUsage.Text.Should().Contain("Example:")
            .And.Contain("create-ai-structure --type webApp --repo my-app");
    }
}
