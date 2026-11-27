using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Cli.ConsoleIo;
using CreateAiStructure.Cli.Parsing;
using CreateAiStructure.Infrastructure.Process;
using CreateAiStructure.Infrastructure.Provisioning;
using CreateAiStructure.Infrastructure.Runtime;
using CreateAiStructure.Infrastructure.T3;
using CreateAiStructure.Infrastructure.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace CreateAiStructure.Cli.Composition;

/// <summary>The CLI composition root: registers every service the tool needs.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all CLI, Application and Infrastructure services as singletons.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="streams">The console streams the CLI writes to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public static IServiceCollection AddCreateAiStructureServices(this IServiceCollection services, ConsoleStreams streams)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(streams);

        services.AddSingleton<IFileSystem, SystemFileSystem>();
        services.AddSingleton<IEnvironmentProvider, SystemEnvironmentProvider>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDelayProvider, SystemDelayProvider>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IToolValidator, ToolValidator>();
        services.AddSingleton<IRepositoryBootstrapper, RepositoryBootstrapper>();
        services.AddSingleton<ITemplateResourceProvider>(_ => new EmbeddedTemplateResourceProvider());
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddSingleton<ITemplateMaterializer, TemplateMaterializer>();
        services.AddSingleton<IT3DesktopExecutableLocator, T3DesktopExecutableLocator>();
        services.AddSingleton<IDetachedProcessLauncher>(_ => new DetachedProcessLauncher());
        services.AddSingleton<IT3AppLauncher, T3AppLauncher>();
        services.AddSingleton<IScaffoldOrchestrator, ScaffoldOrchestrator>();
        services.AddSingleton<CliArgumentParser>();
        services.AddSingleton(streams);
        services.AddSingleton<CliRunner>();

        return services;
    }
}
