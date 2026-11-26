namespace CreateAiStructure.Domain.Common;

/// <summary>
/// An immutable, structurally comparable description of a domain failure.
/// </summary>
public sealed record DomainError
{
    /// <summary>
    /// Creates a new <see cref="DomainError"/>.
    /// </summary>
    /// <param name="code">A stable, machine-readable error code.</param>
    /// <param name="message">A human-readable description of the error.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="code"/> or <paramref name="message"/> is null, empty or whitespace.
    /// </exception>
    public DomainError(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
    }

    /// <summary>Gets the stable, machine-readable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the human-readable description of the error.</summary>
    public string Message { get; }
}
