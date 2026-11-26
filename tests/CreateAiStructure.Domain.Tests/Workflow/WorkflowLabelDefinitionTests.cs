using CreateAiStructure.Domain.Workflow;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Workflow;

public class WorkflowLabelDefinitionTests
{
    private const string ValidName = "epic";
    private const string ValidColor = "5319E7";
    private const string ValidDescription = "DAG Phase / Epic parent issue";

    public static TheoryData<string?> BlankValues => new()
    {
        null,
        "",
        " ",
        "   ",
        "\t",
        "\r\n",
    };

    public static TheoryData<string?> InvalidColors => new()
    {
        "#5319E7",
        "5319E",
        "5319E77",
        "GGGGGG",
        "",
        null,
        "   ",
        "      ",
        "53 9E7",
        "５３１９E7",
        "5319EZ",
        "#5319E",
    };

    public static TheoryData<string, string> ValidColors => new()
    {
        { "5319e7", "5319E7" },
        { "abcdef", "ABCDEF" },
        { "0075CA", "0075CA" },
        { "EDEDED", "EDEDED" },
        { "aBcD09", "ABCD09" },
        { "000000", "000000" },
    };

    // ---- Create: success ----

    [Fact]
    public void Create_succeeds_and_exposes_values()
    {
        var result = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(ValidName);
        result.Value.ColorHex.Should().Be(ValidColor);
        result.Value.Description.Should().Be(ValidDescription);
    }

    [Theory]
    [MemberData(nameof(ValidColors))]
    public void Create_normalizes_color_to_uppercase(string colorHex, string expected)
    {
        var result = WorkflowLabelDefinition.Create(ValidName, colorHex, ValidDescription);

        result.IsSuccess.Should().BeTrue();
        result.Value.ColorHex.Should().Be(expected);
    }

    [Fact]
    public void Create_stores_name_and_description_as_given_without_trimming()
    {
        var result = WorkflowLabelDefinition.Create(" stage:qa ", ValidColor, "  padded description ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(" stage:qa ");
        result.Value.Description.Should().Be("  padded description ");
    }

    // ---- Create: failure ----

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void Create_fails_when_name_is_blank(string? name)
    {
        var result = WorkflowLabelDefinition.Create(name, ValidColor, ValidDescription);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("WorkflowLabel.NameRequired");
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(BlankValues))]
    public void Create_fails_when_description_is_blank(string? description)
    {
        var result = WorkflowLabelDefinition.Create(ValidName, ValidColor, description);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("WorkflowLabel.DescriptionRequired");
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(InvalidColors))]
    public void Create_fails_when_color_is_invalid(string? colorHex)
    {
        var result = WorkflowLabelDefinition.Create(ValidName, colorHex, ValidDescription);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("WorkflowLabel.InvalidColor");
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_validates_name_before_color_and_description()
    {
        var result = WorkflowLabelDefinition.Create(null, "#bad", null);

        result.Error.Code.Should().Be("WorkflowLabel.NameRequired");
    }

    [Fact]
    public void Create_validates_color_before_description()
    {
        var result = WorkflowLabelDefinition.Create(ValidName, "#bad", null);

        result.Error.Code.Should().Be("WorkflowLabel.InvalidColor");
    }

    // ---- Equality ----

    [Fact]
    public void Equals_is_true_for_the_same_values()
    {
        var left = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription).Value;
        var right = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription).Value;

        left.Equals(right).Should().BeTrue();
        left.Equals((object)right).Should().BeTrue();
        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Equals_is_true_when_colors_differ_only_by_input_case()
    {
        var left = WorkflowLabelDefinition.Create(ValidName, "5319e7", ValidDescription).Value;
        var right = WorkflowLabelDefinition.Create(ValidName, "5319E7", ValidDescription).Value;

        left.Equals(right).Should().BeTrue();
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Equals_is_false_when_names_differ()
    {
        var left = WorkflowLabelDefinition.Create("epic", ValidColor, ValidDescription).Value;
        var right = WorkflowLabelDefinition.Create("stage:qa", ValidColor, ValidDescription).Value;

        left.Equals(right).Should().BeFalse();
        left.Equals((object)right).Should().BeFalse();
        (left == right).Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact]
    public void Equals_is_ordinal_and_case_sensitive_for_name()
    {
        var left = WorkflowLabelDefinition.Create("epic", ValidColor, ValidDescription).Value;
        var right = WorkflowLabelDefinition.Create("Epic", ValidColor, ValidDescription).Value;

        left.Equals(right).Should().BeFalse();
        (left == right).Should().BeFalse();
    }

    [Fact]
    public void Equals_is_false_when_colors_differ()
    {
        var left = WorkflowLabelDefinition.Create(ValidName, "5319E7", ValidDescription).Value;
        var right = WorkflowLabelDefinition.Create(ValidName, "0075CA", ValidDescription).Value;

        left.Equals(right).Should().BeFalse();
        (left == right).Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact]
    public void Equals_is_false_when_descriptions_differ()
    {
        var left = WorkflowLabelDefinition.Create(ValidName, ValidColor, "one").Value;
        var right = WorkflowLabelDefinition.Create(ValidName, ValidColor, "two").Value;

        left.Equals(right).Should().BeFalse();
        (left == right).Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact]
    public void Equals_is_ordinal_and_case_sensitive_for_description()
    {
        var left = WorkflowLabelDefinition.Create(ValidName, ValidColor, "description").Value;
        var right = WorkflowLabelDefinition.Create(ValidName, ValidColor, "Description").Value;

        left.Equals(right).Should().BeFalse();
        (left == right).Should().BeFalse();
    }

    [Fact]
    public void Equals_is_true_for_the_same_instance()
    {
        var label = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription).Value;
        var same = label;

        label.Equals(same).Should().BeTrue();
        (label == same).Should().BeTrue();
    }

    [Fact]
    public void Equals_typed_is_false_for_null()
    {
        var label = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription).Value;

        label.Equals((WorkflowLabelDefinition?)null).Should().BeFalse();
    }

    [Fact]
    public void Equals_object_is_false_for_null()
    {
        var label = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription).Value;

        label.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void Equals_object_is_false_for_a_different_type()
    {
        var label = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription).Value;

        label.Equals((object)ValidName).Should().BeFalse();
    }

    [Fact]
    public void Equality_operators_handle_nulls()
    {
        var label = WorkflowLabelDefinition.Create(ValidName, ValidColor, ValidDescription).Value;
        WorkflowLabelDefinition? none = null;
        WorkflowLabelDefinition? alsoNone = null;

        (label == none).Should().BeFalse();
        (none == label).Should().BeFalse();
        (label != none).Should().BeTrue();
        (none != label).Should().BeTrue();
        (none == alsoNone).Should().BeTrue();
        (none != alsoNone).Should().BeFalse();
    }
}
