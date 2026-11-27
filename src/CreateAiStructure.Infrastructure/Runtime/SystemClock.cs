using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Runtime;

/// <summary>
/// Implements <see cref="IClock"/> over the real system clock.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
