using System.Reflection;
using CreateAiStructure.Cli.Parsing;
using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Parsing;

public class CliCommandTests
{
    [Fact]
    public void Hierarchy_is_closed_to_the_three_sealed_outcomes()
    {
        var derived = typeof(CliCommand).Assembly.GetTypes()
            .Where(type => type != typeof(CliCommand) && typeof(CliCommand).IsAssignableFrom(type));

        derived.Should().BeEquivalentTo([typeof(HelpCommand), typeof(VersionCommand), typeof(ScaffoldCommand)]);
        derived.Should().OnlyContain(type => type.IsSealed);
        typeof(CliCommand).IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void Base_constructor_is_not_accessible_outside_the_assembly()
    {
        var constructors = typeof(CliCommand).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(ctor => ctor.GetParameters().Length == 0);

        constructors.Should().ContainSingle()
            .Which.IsFamilyAndAssembly.Should().BeTrue();
    }

    [Fact]
    public void Help_and_version_commands_have_value_equality()
    {
        new HelpCommand().Should().Be(new HelpCommand());
        new VersionCommand().Should().Be(new VersionCommand());
        new HelpCommand().Should().NotBe(new VersionCommand());
    }

    [Fact]
    public void Scaffold_command_carries_its_request()
    {
        var request = ScaffoldRequest.Create(ProjectType.Api, RepositoryName.Create("svc").Value, "/work").Value;

        var command = new ScaffoldCommand(request);

        command.Request.Should().BeSameAs(request);
        command.Should().Be(new ScaffoldCommand(request));
    }
}
