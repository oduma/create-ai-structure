using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;

namespace CreateAiStructure.Cli.Parsing;

/// <summary>
/// A deterministic, dependency-free parser for the <c>create-ai-structure</c> command line.
/// Flags are matched ordinally and case-sensitively; value validation is delegated to the Domain.
/// </summary>
/// <remarks>
/// Any <c>--help</c>/<c>-h</c> token yields <see cref="HelpCommand"/>; otherwise any <c>--version</c>/<c>-v</c>
/// token yields <see cref="VersionCommand"/>. Both take precedence over every other error.
/// Options accept the space form (<c>--type api</c>) and the equals form (<c>--type=api</c>, split on the first <c>=</c>).
/// In the space form the next token is always taken as the value, even when it starts with <c>--</c>;
/// such a value is then rejected (or accepted) by Domain validation.
/// </remarks>
public sealed class CliArgumentParser
{
    private const string TypeFlag = "--type";
    private const string RepoFlag = "--repo";

    private readonly IEnvironmentProvider _environment;

    /// <summary>Creates a parser that reads the working directory from <paramref name="environment"/>.</summary>
    /// <param name="environment">The process environment abstraction.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment"/> is null.</exception>
    public CliArgumentParser(IEnvironmentProvider environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>Parses <paramref name="args"/> into a <see cref="CliCommand"/>.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// A successful result with the command, or a failure with a <see cref="CliErrorCodes"/> code
    /// or an unchanged Domain error from <see cref="ProjectType"/>, <see cref="RepositoryName"/> or <see cref="ScaffoldRequest"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is null.</exception>
    public Result<CliCommand> Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return Fail(CliErrorCodes.MissingRequiredOptions, "The --type and --repo options are required.");
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            return Result<CliCommand>.Success(new HelpCommand());
        }

        if (args.Contains("--version") || args.Contains("-v"))
        {
            return Result<CliCommand>.Success(new VersionCommand());
        }

        return ParseScaffold(args);
    }

    private Result<CliCommand> ParseScaffold(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < args.Length; index++)
        {
            var option = ReadOption(args, ref index);
            if (option.IsFailure)
            {
                return Result<CliCommand>.Failure(option.Error);
            }

            var (flag, value) = option.Value;
            if (!values.TryAdd(flag, value))
            {
                return Fail(CliErrorCodes.DuplicateOption, $"The {flag} option was specified more than once.");
            }
        }

        if (!values.TryGetValue(TypeFlag, out var rawType))
        {
            return MissingOption(TypeFlag);
        }

        if (!values.TryGetValue(RepoFlag, out var rawRepo))
        {
            return MissingOption(RepoFlag);
        }

        return CreateScaffoldCommand(rawType, rawRepo);
    }

    private static Result<(string Flag, string Value)> ReadOption(string[] args, ref int index)
    {
        var token = args[index];

        if (IsOptionFlag(token))
        {
            if (index + 1 >= args.Length)
            {
                return MissingValue(token);
            }

            index++;
            return Result<(string, string)>.Success((token, args[index]));
        }

        var separator = token.IndexOf('=');
        if (separator >= 0 && IsOptionFlag(token[..separator]))
        {
            var flag = token[..separator];
            var value = token[(separator + 1)..];
            return value.Length == 0
                ? MissingValue(flag)
                : Result<(string, string)>.Success((flag, value));
        }

        return Result<(string, string)>.Failure(
            new DomainError(CliErrorCodes.UnknownArgument, $"Unknown argument '{token}'."));
    }

    private Result<CliCommand> CreateScaffoldCommand(string rawType, string rawRepo)
    {
        var projectType = ProjectType.Parse(rawType);
        if (projectType.IsFailure)
        {
            return Result<CliCommand>.Failure(projectType.Error);
        }

        var repositoryName = RepositoryName.Create(rawRepo);
        if (repositoryName.IsFailure)
        {
            return Result<CliCommand>.Failure(repositoryName.Error);
        }

        var request = ScaffoldRequest.Create(
            projectType.Value, repositoryName.Value, _environment.GetCurrentDirectory());

        return request.IsSuccess
            ? Result<CliCommand>.Success(new ScaffoldCommand(request.Value))
            : Result<CliCommand>.Failure(request.Error);
    }

    private static bool IsOptionFlag(string token) =>
        string.Equals(token, TypeFlag, StringComparison.Ordinal) ||
        string.Equals(token, RepoFlag, StringComparison.Ordinal);

    private static Result<(string Flag, string Value)> MissingValue(string flag) =>
        Result<(string, string)>.Failure(
            new DomainError(CliErrorCodes.MissingOptionValue, $"The {flag} option requires a value."));

    private static Result<CliCommand> MissingOption(string flag) =>
        Fail(CliErrorCodes.MissingRequiredOption, $"The {flag} option is required.");

    private static Result<CliCommand> Fail(string code, string message) =>
        Result<CliCommand>.Failure(new DomainError(code, message));
}
