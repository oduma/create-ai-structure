using CreateAiStructure.Cli.Composition;
using CreateAiStructure.Cli.ConsoleIo;
using Microsoft.Extensions.DependencyInjection;

namespace CreateAiStructure.Cli;

/// <summary>
/// Hosts a single CLI invocation: builds the composition root, resolves <see cref="CliRunner"/> once and runs it.
/// This is the only place a service is resolved from the container.
/// </summary>
public static class CliHost
{
    /// <summary>Runs the CLI for <paramref name="args"/> against <paramref name="streams"/>.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="streams">The console streams to write to.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>One of the <see cref="CliExitCodes"/> values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> or <paramref name="streams"/> is null.</exception>
    public static async Task<int> RunAsync(string[] args, ConsoleStreams streams, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(streams);

        await using var provider = CliServiceProviderFactory.Build(
            new ServiceCollection().AddCreateAiStructureServices(streams));

        var runner = provider.GetRequiredService<CliRunner>();
        return await runner.RunAsync(args, cancellationToken);
    }
}
