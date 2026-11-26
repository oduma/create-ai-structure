namespace CreateAiStructure.Domain.Common;

/// <summary>
/// The immutable outcome of an operation that produces no value: either success or a <see cref="DomainError"/>.
/// </summary>
public sealed class Result
{
    private readonly DomainError? _error;

    private Result(DomainError? error) => _error = error;

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => _error is null;

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the error describing the failure.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public DomainError Error =>
        _error ?? throw new InvalidOperationException("A successful result has no error.");

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => new(null);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    public static Result Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
}
