using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Cli.Composition;
using CreateAiStructure.Cli.ConsoleIo;
using CreateAiStructure.Cli.Parsing;
using CreateAiStructure.Infrastructure.Process;
using CreateAiStructure.Infrastructure.Provisioning;
using CreateAiStructure.Infrastructure.Runtime;
using CreateAiStructure.Infrastructure.T3;
using CreateAiStructure.Infrastructure.Templates;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CreateAiStructure.Cli.Tests.Composition;

public sealed class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly ConsoleStreams _streams = new(new StringWriter(), new StringWriter());
    private readonly ServiceProvider _provider;

    public ServiceCollectionExtensionsTests()
    {
        _provider = CliServiceProviderFactory.Build(new ServiceCollection().AddCreateAiStructureServices(_streams));
    }

    public void Dispose() => _provider.Dispose();

    public static TheoryData<Type, Type> ServiceMappings => new()
    {
        { typeof(IFileSystem), typeof(SystemFileSystem) },
        { typeof(IEnvironmentProvider), typeof(SystemEnvironmentProvider) },
        { typeof(IClock), typeof(SystemClock) },
        { typeof(IDelayProvider), typeof(SystemDelayProvider) },
        { typeof(IProcessRunner), typeof(ProcessRunner) },
        { typeof(IToolValidator), typeof(ToolValidator) },
        { typeof(IRepositoryBootstrapper), typeof(RepositoryBootstrapper) },
        { typeof(ITemplateResourceProvider), typeof(EmbeddedTemplateResourceProvider) },
        { typeof(ITemplateRenderer), typeof(TemplateRenderer) },
        { typeof(ITemplateMaterializer), typeof(TemplateMaterializer) },
        { typeof(IT3DesktopExecutableLocator), typeof(T3DesktopExecutableLocator) },
        { typeof(IDetachedProcessLauncher), typeof(DetachedProcessLauncher) },
        { typeof(IT3AppLauncher), typeof(T3AppLauncher) },
        { typeof(IScaffoldOrchestrator), typeof(ScaffoldOrchestrator) },
        { typeof(CliArgumentParser), typeof(CliArgumentParser) },
        { typeof(ConsoleStreams), typeof(ConsoleStreams) },
        { typeof(CliRunner), typeof(CliRunner) },
    };

    public static TheoryData<Type> ServiceTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var row in ServiceMappings)
        {
            data.Add((Type)row[0]);
        }

        return data;
    }

    [Fact]
    public void Build_succeeds_with_build_time_validation_of_every_registration()
    {
        var act = () =>
        {
            using var provider = CliServiceProviderFactory.Build(
                new ServiceCollection().AddCreateAiStructureServices(_streams));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Build_rejects_a_registration_whose_dependency_is_missing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CliRunner>();

        var act = () => CliServiceProviderFactory.Build(services);

        act.Should().Throw<AggregateException>();
    }

    [Fact]
    public void Build_validates_scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IClock, SystemClock>();
        using var provider = CliServiceProviderFactory.Build(services);

        var act = () => provider.GetService<IClock>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(ServiceMappings))]
    public void Service_resolves_to_its_exact_concrete_type(Type serviceType, Type implementationType)
    {
        var instance = _provider.GetService(serviceType);

        instance.Should().NotBeNull();
        instance!.GetType().Should().Be(implementationType);
    }

    [Theory]
    [MemberData(nameof(ServiceTypes))]
    public void Service_is_a_singleton(Type serviceType)
    {
        _provider.GetService(serviceType).Should().BeSameAs(_provider.GetService(serviceType));
    }

    [Fact]
    public void Every_registration_is_a_singleton()
    {
        var services = new ServiceCollection().AddCreateAiStructureServices(_streams);

        services.Should().HaveCount(ServiceMappings.Count());
        services.Should().OnlyContain(descriptor => descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Every_registered_service_type_is_covered_by_the_expected_mappings()
    {
        var services = new ServiceCollection().AddCreateAiStructureServices(_streams);

        services.Select(descriptor => descriptor.ServiceType)
            .Should().BeEquivalentTo(ServiceMappings.Select(row => (Type)row[0]));
    }

    [Fact]
    public void Console_streams_resolve_to_the_supplied_instance()
    {
        _provider.GetRequiredService<ConsoleStreams>().Should().BeSameAs(_streams);
    }

    [Fact]
    public void Add_services_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddCreateAiStructureServices(_streams).Should().BeSameAs(services);
    }

    [Fact]
    public void Add_services_throws_when_services_is_null()
    {
        var act = () => ((IServiceCollection)null!).AddCreateAiStructureServices(_streams);

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void Add_services_throws_when_streams_is_null()
    {
        var act = () => new ServiceCollection().AddCreateAiStructureServices(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("streams");
    }

    [Fact]
    public void Build_throws_when_services_is_null()
    {
        var act = () => CliServiceProviderFactory.Build(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
}
