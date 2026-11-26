using System.Reflection;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Architecture;

public class InfrastructureDependencyTests
{
    private const string InfrastructureAssemblyName = "CreateAiStructure.Infrastructure";

    [Fact]
    public void Infrastructure_project_references_only_the_application_project()
    {
        var project = ProjectFile.Load("src", InfrastructureAssemblyName);

        project.ProjectReferences.Should().Equal("CreateAiStructure.Application");
    }

    [Fact]
    public void Infrastructure_assembly_does_not_reference_the_cli()
    {
        var infrastructure = Assembly.Load(new AssemblyName(InfrastructureAssemblyName));

        infrastructure.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain("create-ai-structure");
    }
}
