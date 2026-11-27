namespace CreateAiStructure.Application.Orchestration;

/// <summary>
/// Stable, machine-readable <see cref="Domain.Common.DomainError"/> codes produced by the scaffolding orchestration (FR-2).
/// </summary>
internal static class OrchestrationErrorCodes
{
    /// <summary>One or more required command-line tools are not available on PATH.</summary>
    public const string MissingRequiredTools = "Orchestration.MissingRequiredTools";
}
