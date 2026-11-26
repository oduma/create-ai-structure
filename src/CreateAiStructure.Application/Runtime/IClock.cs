namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// Abstracts the system clock.
/// </summary>
public interface IClock
{
    /// <summary>Gets the current date and time in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
