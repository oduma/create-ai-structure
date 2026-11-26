using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Application.Launching;

/// <summary>
/// Launches the T3 Code desktop app for a scaffolded repository.
/// </summary>
public interface IT3AppLauncher
{
    /// <summary>Launches T3 Code for the repository.</summary>
    /// <param name="repositoryDirectory">The local repository directory.</param>
    /// <param name="cancellationToken">A token that cancels the launch.</param>
    /// <returns>A successful result with the launch outcome, or a failure describing why.</returns>
    Task<Result<T3LaunchOutcome>> LaunchAsync(string repositoryDirectory, CancellationToken cancellationToken = default);
}
