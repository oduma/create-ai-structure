using System.Reflection;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Architecture;

public class CliAssemblyTests
{
    private const string CliAssemblyName = "create-ai-structure";

    private static Assembly LoadCli() => Assembly.Load(new AssemblyName(CliAssemblyName));

    [Fact]
    public void Cli_assembly_is_named_create_ai_structure()
    {
        LoadCli().GetName().Name.Should().Be(CliAssemblyName);
    }

    [Fact]
    public void Cli_assembly_has_an_entry_point()
    {
        LoadCli().EntryPoint.Should().NotBeNull();
    }

    [Fact]
    public void Invoking_the_entry_point_returns_exit_code_zero()
    {
        var entryPoint = LoadCli().EntryPoint!;
        object?[]? arguments = entryPoint.GetParameters().Length == 0 ? null : [Array.Empty<string>()];

        var exitCode = entryPoint.Invoke(null, arguments);

        exitCode.Should().Be(0);
    }

    [Fact]
    public void Cli_project_is_an_executable_named_create_ai_structure()
    {
        var project = ProjectFile.Load("src", "CreateAiStructure.Cli");

        project.Property("OutputType").Should().Be("Exe");
        project.Property("AssemblyName").Should().Be(CliAssemblyName);
    }

    [Fact]
    public void Cli_project_references_application_and_infrastructure()
    {
        var project = ProjectFile.Load("src", "CreateAiStructure.Cli");

        project.ProjectReferences.Should().BeEquivalentTo(
            "CreateAiStructure.Application", "CreateAiStructure.Infrastructure");
    }
}
