using System.Reflection;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Architecture;

public class DomainDependencyTests
{
    private const string DomainAssemblyName = "CreateAiStructure.Domain";

    private static readonly string[] MockingFrameworks =
        ["NSubstitute", "Moq", "FakeItEasy", "JustMock", "Telerik.JustMock", "Rhino.Mocks", "NMock"];

    [Fact]
    public void Domain_project_has_no_project_references()
    {
        var project = ProjectFile.Load("src", DomainAssemblyName);

        project.ProjectReferences.Should().BeEmpty("the Domain layer must have zero dependencies");
    }

    [Fact]
    public void Domain_project_has_no_package_references()
    {
        var project = ProjectFile.Load("src", DomainAssemblyName);

        project.PackageReferences.Should().BeEmpty("the Domain layer must have zero NuGet dependencies");
    }

    [Fact]
    public void Domain_assembly_references_only_framework_assemblies()
    {
        var domain = Assembly.Load(new AssemblyName(DomainAssemblyName));

        domain.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().OnlyContain(name => IsFrameworkAssembly(name),
                "the Domain assembly may only depend on the .NET base class library");
    }

    [Fact]
    public void Domain_tests_project_does_not_reference_a_mocking_framework()
    {
        var project = ProjectFile.Load("tests", "CreateAiStructure.Domain.Tests");

        project.PackageReferences.Should().NotContain(
            package => MockingFrameworks.Contains(package, StringComparer.OrdinalIgnoreCase),
            "Domain entities are tested directly, without mocks");
    }

    private static bool IsFrameworkAssembly(string? name) =>
        name is not null
        && (name == "System"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name == "netstandard"
            || name == "mscorlib");
}
