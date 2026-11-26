namespace CreateAiStructure.Application.Runtime;

/// <summary>
/// Abstracts access to the process environment.
/// </summary>
public interface IEnvironmentProvider
{
    /// <summary>Gets the value of an environment variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or <see langword="null"/> if the variable is not set.</returns>
    string? GetEnvironmentVariable(string name);

    /// <summary>Gets the path of the current user's local application data folder.</summary>
    /// <returns>The local application data path.</returns>
    string GetLocalApplicationDataPath();

    /// <summary>Gets the current working directory.</summary>
    /// <returns>The current working directory path.</returns>
    string GetCurrentDirectory();
}
