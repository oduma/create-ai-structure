using System.Reflection;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Application.Tests.Architecture;

public class ApplicationDependencyTests
{
    private const string ApplicationAssemblyName = "CreateAiStructure.Application";

    [Fact]
    public void Application_project_references_only_the_domain_project()
    {
        var project = ProjectFile.Load("src", ApplicationAssemblyName);

        project.ProjectReferences.Should().Equal("CreateAiStructure.Domain");
    }

    [Fact]
    public void Application_project_has_no_package_references()
    {
        var project = ProjectFile.Load("src", ApplicationAssemblyName);

        project.PackageReferences.Should().BeEmpty("the Application layer must not depend on NuGet packages");
    }

    [Fact]
    public void Application_assembly_does_not_reference_outer_layers()
    {
        var application = Assembly.Load(new AssemblyName(ApplicationAssemblyName));

        application.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain(["CreateAiStructure.Infrastructure", "create-ai-structure"]);
    }
}
