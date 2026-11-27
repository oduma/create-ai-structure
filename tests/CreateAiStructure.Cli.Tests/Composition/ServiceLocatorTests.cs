using System.Reflection;
using CreateAiStructure.Cli.Composition;
using FluentAssertions;

namespace CreateAiStructure.Cli.Tests.Composition;

public class ServiceLocatorTests
{
    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly CliAssembly = typeof(CliHost).Assembly;

    private static bool IsServiceProvider(Type type) => typeof(IServiceProvider).IsAssignableFrom(type);

    private static bool IsCompositionRoot(Type type)
    {
        var outermost = type;
        while (outermost.DeclaringType is not null)
        {
            outermost = outermost.DeclaringType;
        }

        return outermost == typeof(CliHost) || outermost.Namespace == typeof(CliServiceProviderFactory).Namespace;
    }

    [Fact]
    public void Composition_root_is_detected_by_the_scan()
    {
        // Guards the scan itself: CliHost holds the provider, so an unfiltered scan must find it.
        CliAssembly.GetTypes()
            .Where(type => type == typeof(CliHost) || type.DeclaringType == typeof(CliHost))
            .SelectMany(type => type.GetFields(AllMembers))
            .Should().Contain(field => IsServiceProvider(field.FieldType));
    }

    [Fact]
    public void No_type_outside_the_composition_root_takes_a_service_provider_in_a_constructor()
    {
        var offenders = CliAssembly.GetTypes()
            .Where(type => !IsCompositionRoot(type))
            .SelectMany(type => type.GetConstructors(AllMembers))
            .Where(ctor => ctor.GetParameters().Any(parameter => IsServiceProvider(parameter.ParameterType)))
            .Select(ctor => ctor.DeclaringType!.FullName);

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void No_type_outside_the_composition_root_holds_a_service_provider_field()
    {
        var offenders = CliAssembly.GetTypes()
            .Where(type => !IsCompositionRoot(type))
            .SelectMany(type => type.GetFields(AllMembers))
            .Where(field => IsServiceProvider(field.FieldType))
            .Select(field => $"{field.DeclaringType!.FullName}.{field.Name}");

        offenders.Should().BeEmpty();
    }
}
