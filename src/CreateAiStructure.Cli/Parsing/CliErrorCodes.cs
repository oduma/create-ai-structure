namespace CreateAiStructure.Cli.Parsing;

/// <summary>Stable, machine-readable error codes produced by <see cref="CliArgumentParser"/>.</summary>
public static class CliErrorCodes
{
    /// <summary>No arguments were supplied at all.</summary>
    public const string MissingRequiredOptions = "Cli.MissingRequiredOptions";

    /// <summary>A single required option (<c>--type</c> or <c>--repo</c>) was not supplied.</summary>
    public const string MissingRequiredOption = "Cli.MissingRequiredOption";

    /// <summary>An option was supplied without a value.</summary>
    public const string MissingOptionValue = "Cli.MissingOptionValue";

    /// <summary>An option was supplied more than once.</summary>
    public const string DuplicateOption = "Cli.DuplicateOption";

    /// <summary>A token was not recognised as a supported option.</summary>
    public const string UnknownArgument = "Cli.UnknownArgument";
}
