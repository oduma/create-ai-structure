using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;

namespace CreateAiStructure.Application.Orchestration;

/// <summary>
/// Runs the end-to-end scaffolding workflow.
/// </summary>
public interface IScaffoldOrchestrator
{
    /// <summary>Executes the scaffolding workflow for a validated request.</summary>
    /// <param name="request">The validated scaffold request.</param>
    /// <param name="cancellationToken">A token that cancels the workflow.</param>
    /// <returns>A successful result with the scaffold outcome, or a failure describing the first failing step.</returns>
    Task<Result<ScaffoldResult>> ExecuteAsync(ScaffoldRequest request, CancellationToken cancellationToken = default);
}
