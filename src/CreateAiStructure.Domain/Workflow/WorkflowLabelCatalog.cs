namespace CreateAiStructure.Domain.Workflow;

/// <summary>
/// The fixed set of GitHub labels that drive the multi-agent workflow state machine, in state-machine order.
/// </summary>
public static class WorkflowLabelCatalog
{
    /// <summary>Gets the eight workflow labels in state-machine order. The list is read-only and cached.</summary>
    public static IReadOnlyList<WorkflowLabelDefinition> All { get; } = Array.AsReadOnly(
    [
        Label("epic", "5319E7", "DAG Phase / Epic parent issue"),
        Label("status:backlog", "EDEDED", "Waiting on upstream dependencies or WIP slot"),
        Label("status:in-progress", "0075CA", "Active Epic counting toward max WIP limit"),
        Label("stage:po-spec", "D4C5F9", "PO agent writing detailed requirements and acceptance criteria"),
        Label("stage:ready-for-arch", "0E8A16", "Spec finalized and ready for Claude Architect /plan"),
        Label("stage:implementing", "1D76DB", "Claude Architect and subagents executing Sub-Issues"),
        Label("stage:qa", "FBCA04", "PR open; Antigravity QA agent running verification"),
        Label("stage:human-review", "D93F0B", "QA report attached; awaiting human approval"),
    ]);

    private static WorkflowLabelDefinition Label(string name, string colorHex, string description) =>
        WorkflowLabelDefinition.Create(name, colorHex, description).Value;
}
