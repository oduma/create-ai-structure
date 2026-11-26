namespace CreateAiStructure.Domain.Common;

/// <summary>
/// The immutable outcome of an operation that produces a <typeparamref name="T"/>:
/// either a value or a <see cref="DomainError"/>.
/// </summary>
/// <typeparam name="T">The type of the value produced on success.</typeparam>
public sealed class Result<T>
{
    private readonly T _value;
    private readonly DomainError? _error;

    private Result(T value, DomainError? error)
    {
        _value = value;
        _error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => _error is null;

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the value produced by the operation.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a failure.</exception>
    public T Value =>
        IsSuccess ? _value : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>Gets the error describing the failure.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public DomainError Error =>
        _error ?? throw new InvalidOperationException("A successful result has no error.");

    /// <summary>Creates a successful result carrying <paramref name="value"/>, which may be null for nullable types.</summary>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    public static Result<T> Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default!, error);
    }
}
