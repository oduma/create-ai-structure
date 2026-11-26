namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// Abstracts asynchronous waiting so that polling and retry logic can be tested without real delays.
/// </summary>
public interface IDelayProvider
{
    /// <summary>Waits for the given duration.</summary>
    /// <param name="delay">How long to wait.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes after the delay.</returns>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
