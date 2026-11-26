using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Workflow;

namespace CreateAiStructure.Application.Provisioning;

/// <summary>
/// Creates the GitHub repository, clones it locally and finalizes it after scaffolding.
/// </summary>
public interface IRepositoryBootstrapper
{
    /// <summary>Creates the GitHub repository and clones it into the working directory.</summary>
    /// <param name="repositoryName">The validated repository name.</param>
    /// <param name="workingDirectory">The directory the repository is cloned into.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A successful result with the clone directory and slug, or a failure describing why.</returns>
    Task<Result<RepositoryCloneResult>> CreateAndCloneRepositoryAsync(
        RepositoryName repositoryName,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>Finalizes the scaffolded repository, including creating the workflow labels.</summary>
    /// <param name="repositoryDirectory">The local clone directory.</param>
    /// <param name="labels">The workflow labels to create.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A successful result, or a failure describing why.</returns>
    Task<Result> FinalizeRepositoryAsync(
        string repositoryDirectory,
        IReadOnlyList<WorkflowLabelDefinition> labels,
        CancellationToken cancellationToken = default);
}
