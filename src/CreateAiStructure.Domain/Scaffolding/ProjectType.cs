using System.Collections.ObjectModel;
using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Domain.Scaffolding;

/// <summary>
/// The closed set of target project types that can be scaffolded.
/// Instances are compared by their <see cref="CanonicalName"/>.
/// </summary>
public sealed class ProjectType : IEquatable<ProjectType>
{
    // Declaration order matters: static fields initialize in textual order,
    // so the instances must precede the collections and strings derived from them.

    /// <summary>A web application project (<c>webApp</c>).</summary>
    public static readonly ProjectType WebApp = new("webApp");

    /// <summary>An HTTP API project (<c>api</c>).</summary>
    public static readonly ProjectType Api = new("api");

    /// <summary>A console application project (<c>console</c>).</summary>
    public static readonly ProjectType Console = new("console");

    /// <summary>A .NET MAUI application project (<c>MAUI</c>).</summary>
    public static readonly ProjectType Maui = new("MAUI");

    private static readonly ProjectType[] Values = [WebApp, Api, Console, Maui];

    /// <summary>Gets every supported project type, in canonical order.</summary>
    public static readonly IReadOnlyList<ProjectType> All = Array.AsReadOnly(Values);

    private static readonly string SupportedValues = string.Join(", ", Values.Select(v => v.CanonicalName));

    private ProjectType(string canonicalName) => CanonicalName = canonicalName;

    /// <summary>Gets the canonical, user-facing name of the project type.</summary>
    public string CanonicalName { get; }

    /// <summary>
    /// Parses <paramref name="rawValue"/> into a <see cref="ProjectType"/> using an exact,
    /// case-insensitive match against the canonical names. The input is not trimmed.
    /// </summary>
    /// <param name="rawValue">The raw project type supplied by the user.</param>
    /// <returns>
    /// A successful result with the matching project type, or a failure with code
    /// <c>ProjectType.Required</c> (blank input) or <c>ProjectType.Unsupported</c> (unknown input).
    /// </returns>
    public static Result<ProjectType> Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Result<ProjectType>.Failure(new DomainError(
                "ProjectType.Required",
                $"Project type is required. Supported values: {SupportedValues}."));
        }

        foreach (var projectType in Values)
        {
            if (string.Equals(projectType.CanonicalName, rawValue, StringComparison.OrdinalIgnoreCase))
            {
                return Result<ProjectType>.Success(projectType);
            }
        }

        return Result<ProjectType>.Failure(new DomainError(
            "ProjectType.Unsupported",
            $"Unsupported project type '{rawValue}'. Supported values: {SupportedValues}."));
    }

    /// <inheritdoc />
    public bool Equals(ProjectType? other) =>
        other is not null && string.Equals(CanonicalName, other.CanonicalName, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ProjectType other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(CanonicalName);

    /// <summary>Returns the <see cref="CanonicalName"/>.</summary>
    public override string ToString() => CanonicalName;

    /// <summary>Determines whether two project types are equal; two nulls are equal.</summary>
    public static bool operator ==(ProjectType? left, ProjectType? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Determines whether two project types differ.</summary>
    public static bool operator !=(ProjectType? left, ProjectType? right) => !(left == right);
}
