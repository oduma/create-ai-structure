using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Domain.Scaffolding;

/// <summary>
/// A validated name for the target repository, safe to use as a single directory name
/// on every supported platform. Instances are compared ordinally (case-sensitive) by <see cref="Value"/>.
/// </summary>
public sealed class RepositoryName : IEquatable<RepositoryName>
{
    /// <summary>The maximum number of characters allowed in a repository name.</summary>
    public const int MaxLength = 100;

    private const string GitSuffix = ".git";

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private RepositoryName(string value) => Value = value;

    /// <summary>Gets the repository name exactly as supplied, with its original casing.</summary>
    public string Value { get; }

    /// <summary>
    /// Validates <paramref name="rawValue"/> as a repository name. Rules are applied in a fixed
    /// order and the first failing rule determines the error. The input is not trimmed.
    /// </summary>
    /// <param name="rawValue">The raw repository name supplied by the user.</param>
    /// <returns>
    /// A successful result with the repository name, or a failure with one of the codes
    /// <c>RepositoryName.Required</c>, <c>RepositoryName.TooLong</c>, <c>RepositoryName.InvalidCharacters</c>,
    /// <c>RepositoryName.LeadingHyphen</c>, <c>RepositoryName.PathTraversal</c>,
    /// <c>RepositoryName.TrailingPeriod</c>, <c>RepositoryName.GitSuffix</c> or <c>RepositoryName.ReservedName</c>.
    /// </returns>
    public static Result<RepositoryName> Create(string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return Fail("Required", "Repository name is required.");
        }

        if (rawValue.Length > MaxLength)
        {
            return Fail("TooLong", $"Repository name '{rawValue}' is too long: it must be at most {MaxLength} characters.");
        }

        if (!ContainsOnlyAllowedCharacters(rawValue))
        {
            return Fail("InvalidCharacters", $"Repository name '{rawValue}' contains invalid characters: only ASCII letters, digits, '.', '_' and '-' are allowed.");
        }

        if (rawValue[0] == '-')
        {
            return Fail("LeadingHyphen", $"Repository name '{rawValue}' must not start with a leading hyphen.");
        }

        if (rawValue == "." || rawValue.Contains("..", StringComparison.Ordinal))
        {
            return Fail("PathTraversal", $"Repository name '{rawValue}' is not allowed: it must not be '.' or contain '..' (path traversal).");
        }

        if (rawValue[^1] == '.')
        {
            return Fail("TrailingPeriod", $"Repository name '{rawValue}' must not end with a trailing period.");
        }

        if (rawValue.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("GitSuffix", $"Repository name '{rawValue}' must not end with the '{GitSuffix}' suffix.");
        }

        if (IsReservedName(rawValue))
        {
            return Fail("ReservedName", $"Repository name '{rawValue}' is a reserved device name on Windows.");
        }

        return Result<RepositoryName>.Success(new RepositoryName(rawValue));
    }

    /// <inheritdoc />
    public bool Equals(RepositoryName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RepositoryName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns the <see cref="Value"/>.</summary>
    public override string ToString() => Value;

    /// <summary>Determines whether two repository names are equal; two nulls are equal.</summary>
    public static bool operator ==(RepositoryName? left, RepositoryName? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Determines whether two repository names differ.</summary>
    public static bool operator !=(RepositoryName? left, RepositoryName? right) => !(left == right);

    private static Result<RepositoryName> Fail(string rule, string message) =>
        Result<RepositoryName>.Failure(new DomainError($"RepositoryName.{rule}", message));

    private static bool ContainsOnlyAllowedCharacters(string value)
    {
        foreach (var c in value)
        {
            if (!IsAllowedCharacter(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedCharacter(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-';

    private static bool IsReservedName(string value)
    {
        var periodIndex = value.IndexOf('.');
        var stem = periodIndex < 0 ? value : value[..periodIndex];
        return ReservedNames.Contains(stem);
    }
}
