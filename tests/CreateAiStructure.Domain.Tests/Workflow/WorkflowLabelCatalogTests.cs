using CreateAiStructure.Domain.Workflow;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Workflow;

public class WorkflowLabelCatalogTests
{
    public static TheoryData<int, string, string, string> ExpectedLabels => new()
    {
        { 0, "epic", "5319E7", "DAG Phase / Epic parent issue" },
        { 1, "status:backlog", "EDEDED", "Waiting on upstream dependencies or WIP slot" },
        { 2, "status:in-progress", "0075CA", "Active Epic counting toward max WIP limit" },
        { 3, "stage:po-spec", "D4C5F9", "PO agent writing detailed requirements and acceptance criteria" },
        { 4, "stage:ready-for-arch", "0E8A16", "Spec finalized and ready for Claude Architect /plan" },
        { 5, "stage:implementing", "1D76DB", "Claude Architect and subagents executing Sub-Issues" },
        { 6, "stage:qa", "FBCA04", "PR open; Antigravity QA agent running verification" },
        { 7, "stage:human-review", "D93F0B", "QA report attached; awaiting human approval" },
    };

    [Fact]
    public void All_contains_exactly_eight_labels()
    {
        WorkflowLabelCatalog.All.Should().HaveCount(8);
    }

    [Theory]
    [MemberData(nameof(ExpectedLabels))]
    public void All_contains_the_expected_label_at_its_position(
        int index, string name, string colorHex, string description)
    {
        var label = WorkflowLabelCatalog.All[index];

        label.Name.Should().Be(name);
        label.ColorHex.Should().Be(colorHex);
        label.Description.Should().Be(description);
    }

    [Fact]
    public void All_lists_names_in_state_machine_order()
    {
        WorkflowLabelCatalog.All.Select(label => label.Name).Should().Equal(
            "epic",
            "status:backlog",
            "status:in-progress",
            "stage:po-spec",
            "stage:ready-for-arch",
            "stage:implementing",
            "stage:qa",
            "stage:human-review");
    }

    [Fact]
    public void All_has_uppercase_six_character_hex_colors()
    {
        WorkflowLabelCatalog.All.Should().OnlyContain(label =>
            label.ColorHex.Length == 6
            && label.ColorHex.All(char.IsAsciiHexDigit)
            && label.ColorHex == label.ColorHex.ToUpperInvariant());
    }

    [Fact]
    public void All_has_unique_names()
    {
        WorkflowLabelCatalog.All.Select(label => label.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_returns_the_same_instance_on_each_access()
    {
        WorkflowLabelCatalog.All.Should().BeSameAs(WorkflowLabelCatalog.All);
    }

    [Fact]
    public void All_cannot_be_mutated_through_cast()
    {
        var mutable = (IList<WorkflowLabelDefinition>)WorkflowLabelCatalog.All;
        var extra = WorkflowLabelDefinition.Create("extra", "000000", "extra label").Value;

        FluentActions.Invoking(() => mutable.Add(extra)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => mutable[0] = extra).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => mutable.Clear()).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => mutable.RemoveAt(0)).Should().Throw<NotSupportedException>();
    }
}
