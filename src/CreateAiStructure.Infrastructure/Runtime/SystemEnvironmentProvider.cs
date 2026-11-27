using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Runtime;

/// <summary>
/// Implements <see cref="IEnvironmentProvider"/> over the real process environment via <see cref="Environment"/>.
/// </summary>
public sealed class SystemEnvironmentProvider : IEnvironmentProvider
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public string? GetEnvironmentVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Environment.GetEnvironmentVariable(name);
    }

    /// <inheritdoc />
    public string GetLocalApplicationDataPath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <inheritdoc />
    public string GetCurrentDirectory() => Environment.CurrentDirectory;
}
