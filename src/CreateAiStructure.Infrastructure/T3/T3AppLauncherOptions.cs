namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Timing and retry settings used when launching the T3 Code app and waiting for it to become
/// reachable. Instances are immutable and always hold valid values.
/// </summary>
public sealed class T3AppLauncherOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T3AppLauncherOptions"/> class.
    /// </summary>
    /// <param name="initialStartupDelay">The delay to wait after starting the app before the first probe; must be greater than zero.</param>
    /// <param name="retryInterval">The delay between consecutive probes; must be greater than zero.</param>
    /// <param name="maxRetryAttempts">The maximum number of probe attempts; must be at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="initialStartupDelay"/> or <paramref name="retryInterval"/> is zero or negative,
    /// or when <paramref name="maxRetryAttempts"/> is less than one.
    /// </exception>
    public T3AppLauncherOptions(TimeSpan initialStartupDelay, TimeSpan retryInterval, int maxRetryAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialStartupDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRetryAttempts, 1);

        InitialStartupDelay = initialStartupDelay;
        RetryInterval = retryInterval;
        MaxRetryAttempts = maxRetryAttempts;
    }

    /// <summary>
    /// Gets the default options: a two-second initial startup delay, a one-second retry interval
    /// and ten retry attempts. The same instance is returned on every access.
    /// </summary>
    public static T3AppLauncherOptions Default { get; } =
        new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), 10);

    /// <summary>Gets the delay to wait after starting the app before the first probe.</summary>
    public TimeSpan InitialStartupDelay { get; }

    /// <summary>Gets the delay between consecutive probes.</summary>
    public TimeSpan RetryInterval { get; }

    /// <summary>Gets the maximum number of probe attempts.</summary>
    public int MaxRetryAttempts { get; }
}
