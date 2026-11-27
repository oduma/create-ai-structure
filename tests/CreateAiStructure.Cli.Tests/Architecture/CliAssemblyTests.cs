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
    public async Task Invoking_the_entry_point_with_help_returns_exit_code_zero()
    {
        var exitCode = await InvokeEntryPointAsync("--help");

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task Invoking_the_entry_point_without_arguments_returns_usage_error_exit_code_two()
    {
        var exitCode = await InvokeEntryPointAsync();

        exitCode.Should().Be(2);
    }

    // The C# compiler's synthesized entry point for an async top-level program blocks on the Task<int>
    // and returns int, while the user-level method returns Task<int>; both shapes are awaited/unwrapped here.
    private static async Task<int> InvokeEntryPointAsync(params string[] args)
    {
        var result = LoadCli().EntryPoint!.Invoke(null, [args]);

        return result is Task<int> task ? await task : (int)result!;
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
