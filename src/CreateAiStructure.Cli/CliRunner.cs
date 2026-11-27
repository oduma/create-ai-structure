using CreateAiStructure.Application.Orchestration;
using CreateAiStructure.Cli.ConsoleIo;
using CreateAiStructure.Cli.Parsing;
using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Cli;

/// <summary>
/// Parses the command line, dispatches the resulting command and maps every outcome to console output and an exit code.
/// </summary>
/// <remarks>
/// Help and version write to stdout and never invoke the orchestrator; parse and orchestration failures write to stderr.
/// Any <see cref="OperationCanceledException"/> maps to <see cref="CliExitCodes.Cancelled"/>; every other exception is a
/// last-chance <c>Cli.UnexpectedError</c> written to stderr (message only, no stack trace) with
/// <see cref="CliExitCodes.ExecutionFailure"/>. Null-argument guards still throw.
/// </remarks>
public sealed class CliRunner
{
    private readonly CliArgumentParser _parser;
    private readonly IScaffoldOrchestrator _orchestrator;
    private readonly ConsoleStreams _streams;

    /// <summary>Creates the runner.</summary>
    /// <param name="parser">The command-line parser.</param>
    /// <param name="orchestrator">The scaffolding workflow.</param>
    /// <param name="streams">The output streams.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public CliRunner(CliArgumentParser parser, IScaffoldOrchestrator orchestrator, ConsoleStreams streams)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(streams);

        _parser = parser;
        _orchestrator = orchestrator;
        _streams = streams;
    }

    /// <summary>Runs the CLI for <paramref name="args"/>.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="cancellationToken">A token that cancels the scaffolding workflow.</param>
    /// <returns>One of the <see cref="CliExitCodes"/> values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is null.</exception>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            return await DispatchAsync(args, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _streams.Error.WriteLine(CliOutputFormatter.CancelledMessage);
            return CliExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            var error = new DomainError(CliErrorCodes.UnexpectedError, $"An unexpected error occurred: {ex.Message}");
            _streams.Error.WriteLine(CliOutputFormatter.FormatError(error));
            return CliExitCodes.ExecutionFailure;
        }
    }

    private async Task<int> DispatchAsync(string[] args, CancellationToken cancellationToken)
    {
        var parsed = _parser.Parse(args);
        if (parsed.IsFailure)
        {
            _streams.Error.WriteLine(CliOutputFormatter.FormatError(parsed.Error));
            _streams.Error.WriteLine();
            _streams.Error.WriteLine(CliUsage.Text);
            return CliExitCodes.UsageError;
        }

        var command = parsed.Value;
        if (command is HelpCommand)
        {
            _streams.Out.WriteLine(CliUsage.Text);
            return CliExitCodes.Success;
        }

        if (command is VersionCommand)
        {
            var version = CliVersion.Resolve(typeof(CliRunner).Assembly);
            _streams.Out.WriteLine(CliOutputFormatter.FormatVersion(version));
            return CliExitCodes.Success;
        }

        var scaffold = (ScaffoldCommand)command;
        var result = await _orchestrator.ExecuteAsync(scaffold.Request, cancellationToken);
        if (result.IsFailure)
        {
            _streams.Error.WriteLine(CliOutputFormatter.FormatError(result.Error));
            return CliExitCodes.ExecutionFailure;
        }

        foreach (var line in CliOutputFormatter.FormatSuccess(result.Value))
        {
            _streams.Out.WriteLine(line);
        }

        return CliExitCodes.Success;
    }
}
