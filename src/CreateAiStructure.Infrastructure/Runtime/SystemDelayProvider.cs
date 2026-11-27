using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Runtime;

/// <summary>
/// Implements <see cref="IDelayProvider"/> with real waits via <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
/// </summary>
public sealed class SystemDelayProvider : IDelayProvider
{
    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        return Task.Delay(delay, cancellationToken);
    }
}
