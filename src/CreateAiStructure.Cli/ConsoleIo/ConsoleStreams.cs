namespace CreateAiStructure.Cli.ConsoleIo;

/// <summary>
/// The standard output and standard error writers the CLI reports to.
/// Production code passes <see cref="System.Console.Out"/> and <see cref="System.Console.Error"/>; tests pass <see cref="StringWriter"/>s.
/// </summary>
public sealed record ConsoleStreams
{
    /// <summary>Creates the stream pair.</summary>
    /// <param name="out">The writer for normal output (stdout).</param>
    /// <param name="error">The writer for diagnostics (stderr).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="out"/> or <paramref name="error"/> is null.</exception>
    public ConsoleStreams(TextWriter @out, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(@out);
        ArgumentNullException.ThrowIfNull(error);

        Out = @out;
        Error = error;
    }

    /// <summary>Gets the writer for normal output (stdout).</summary>
    public TextWriter Out { get; }

    /// <summary>Gets the writer for diagnostics (stderr).</summary>
    public TextWriter Error { get; }
}
