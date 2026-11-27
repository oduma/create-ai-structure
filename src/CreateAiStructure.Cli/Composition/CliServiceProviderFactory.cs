using Microsoft.Extensions.DependencyInjection;

namespace CreateAiStructure.Cli.Composition;

/// <summary>Builds the validated root <see cref="ServiceProvider"/> for the CLI.</summary>
public static class CliServiceProviderFactory
{
    /// <summary>
    /// Builds a provider from <paramref name="services"/> with scope validation and build-time validation enabled,
    /// so a missing or mis-scoped registration fails immediately.
    /// </summary>
    /// <param name="services">The service registrations.</param>
    /// <returns>The root service provider; the caller owns and disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static ServiceProvider Build(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }
}
