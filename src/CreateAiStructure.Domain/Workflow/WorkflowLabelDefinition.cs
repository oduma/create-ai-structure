using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Domain.Workflow;

/// <summary>
/// A validated GitHub label used by the multi-agent workflow state machine.
/// Instances are compared ordinally (case-sensitive) by <see cref="Name"/>, <see cref="ColorHex"/>
/// and <see cref="Description"/>.
/// </summary>
public sealed class WorkflowLabelDefinition : IEquatable<WorkflowLabelDefinition>
{
    /// <summary>The exact number of hexadecimal characters in a label color.</summary>
    public const int ColorLength = 6;

    private WorkflowLabelDefinition(string name, string colorHex, string description)
    {
        Name = name;
        ColorHex = colorHex;
        Description = description;
    }

    /// <summary>Gets the label name exactly as supplied.</summary>
    public string Name { get; }

    /// <summary>Gets the label color as six uppercase ASCII hexadecimal characters, without a leading '#'.</summary>
    public string ColorHex { get; }

    /// <summary>Gets the label description exactly as supplied.</summary>
    public string Description { get; }

    /// <summary>
    /// Validates and creates a workflow label. Rules are applied in the order name, color, description,
    /// and the first failing rule determines the error. Name and description are not trimmed.
    /// </summary>
    /// <param name="name">The label name; must not be null, empty or whitespace.</param>
    /// <param name="colorHex">The label color: exactly six ASCII hexadecimal characters with no '#'.</param>
    /// <param name="description">The label description; must not be null, empty or whitespace.</param>
    /// <returns>
    /// A successful result with the label (color normalized to uppercase), or a failure with one of the codes
    /// <c>WorkflowLabel.NameRequired</c>, <c>WorkflowLabel.InvalidColor</c> or <c>WorkflowLabel.DescriptionRequired</c>.
    /// </returns>
    public static Result<WorkflowLabelDefinition> Create(string? name, string? colorHex, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fail("NameRequired", "Workflow label name is required.");
        }

        if (!IsValidColor(colorHex))
        {
            return Fail("InvalidColor", $"Workflow label color '{colorHex}' is invalid: it must be exactly {ColorLength} hexadecimal characters with no '#'.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Fail("DescriptionRequired", $"Workflow label '{name}' requires a description.");
        }

        return Result<WorkflowLabelDefinition>.Success(
            new WorkflowLabelDefinition(name, colorHex.ToUpperInvariant(), description));
    }

    /// <inheritdoc />
    public bool Equals(WorkflowLabelDefinition? other) =>
        other is not null
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(ColorHex, other.ColorHex, StringComparison.Ordinal)
        && string.Equals(Description, other.Description, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WorkflowLabelDefinition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(Name),
            StringComparer.Ordinal.GetHashCode(ColorHex),
            StringComparer.Ordinal.GetHashCode(Description));

    /// <summary>Determines whether two labels are equal; two nulls are equal.</summary>
    public static bool operator ==(WorkflowLabelDefinition? left, WorkflowLabelDefinition? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Determines whether two labels differ.</summary>
    public static bool operator !=(WorkflowLabelDefinition? left, WorkflowLabelDefinition? right) => !(left == right);

    private static Result<WorkflowLabelDefinition> Fail(string rule, string message) =>
        Result<WorkflowLabelDefinition>.Failure(new DomainError($"WorkflowLabel.{rule}", message));

    private static bool IsValidColor([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? colorHex) =>
        colorHex is not null && colorHex.Length == ColorLength && colorHex.All(char.IsAsciiHexDigit);
}
