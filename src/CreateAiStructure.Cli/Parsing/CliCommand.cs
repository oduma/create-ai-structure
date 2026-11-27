using CreateAiStructure.Domain.Scaffolding;

namespace CreateAiStructure.Cli.Parsing;

/// <summary>
/// The closed set of outcomes produced by a successful command-line parse.
/// The hierarchy is closed: the constructor is not accessible outside this assembly, so only
/// <see cref="HelpCommand"/>, <see cref="VersionCommand"/> and <see cref="ScaffoldCommand"/> exist.
/// </summary>
public abstract record CliCommand
{
    private protected CliCommand()
    {
    }
}

/// <summary>The user asked for usage help (<c>--help</c> or <c>-h</c>).</summary>
public sealed record HelpCommand : CliCommand;

/// <summary>The user asked for the tool version (<c>--version</c> or <c>-v</c>).</summary>
public sealed record VersionCommand : CliCommand;

/// <summary>The user asked to scaffold a repository with a validated request.</summary>
/// <param name="Request">The validated scaffold request.</param>
public sealed record ScaffoldCommand(ScaffoldRequest Request) : CliCommand;
