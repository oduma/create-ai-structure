using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Running;

public class CliVersionTests
{
    [Fact]
    public void Resolve_returns_the_informational_version_of_the_cli_assembly()
    {
        CliVersion.Resolve(typeof(CliRunner).Assembly).Should().Be("1.0.0");
    }

    [Fact]
    public void Resolve_returns_the_informational_version_when_it_differs_from_the_assembly_version()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("CliVersionTests.Informational") { Version = new Version(1, 2, 3, 4) },
            AssemblyBuilderAccess.Run,
            [
                new CustomAttributeBuilder(
                    typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
                    ["9.9.9-preview"]),
            ]);

        CliVersion.Resolve(assembly).Should().Be("9.9.9-preview");
    }

    [Fact]
    public void Resolve_falls_back_to_the_assembly_version_when_the_informational_version_is_absent()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("CliVersionTests.Fallback") { Version = new Version(2, 3, 4, 5) },
            AssemblyBuilderAccess.Run);

        CliVersion.Resolve(assembly).Should().Be("2.3.4.5");
    }

    [Fact]
    public void Resolve_throws_when_assembly_is_null()
    {
        var act = () => CliVersion.Resolve(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("assembly");
    }
}
