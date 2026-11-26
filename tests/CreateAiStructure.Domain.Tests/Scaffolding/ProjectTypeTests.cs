using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Scaffolding;

public class ProjectTypeTests
{
    private static readonly string[] SupportedNames = ["webApp", "api", "console", "MAUI"];

    public static TheoryData<string, ProjectType> ValidInputs => new()
    {
        { "webApp", ProjectType.WebApp },
        { "webapp", ProjectType.WebApp },
        { "WEBAPP", ProjectType.WebApp },
        { "api", ProjectType.Api },
        { "Api", ProjectType.Api },
        { "API", ProjectType.Api },
        { "console", ProjectType.Console },
        { "CONSOLE", ProjectType.Console },
        { "MAUI", ProjectType.Maui },
        { "maui", ProjectType.Maui },
        { "Maui", ProjectType.Maui },
    };

    public static TheoryData<ProjectType, string> CanonicalNames => new()
    {
        { ProjectType.WebApp, "webApp" },
        { ProjectType.Api, "api" },
        { ProjectType.Console, "console" },
        { ProjectType.Maui, "MAUI" },
    };

    // ---- Parse: success ----

    [Theory]
    [MemberData(nameof(ValidInputs))]
    public void Parse_matches_canonical_names_case_insensitively(string rawValue, ProjectType expected)
    {
        var result = ProjectType.Parse(rawValue);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);
    }

    // ---- Parse: failure ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Parse_fails_with_required_error_for_blank_input(string? rawValue)
    {
        var result = ProjectType.Parse(rawValue);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProjectType.Required");
        result.Error.Message.Should().ContainAll(SupportedNames);
    }

    [Theory]
    [InlineData(" api ")]
    [InlineData("api ")]
    [InlineData("blazor")]
    [InlineData("desktop")]
    public void Parse_fails_with_unsupported_error_for_unknown_input(string rawValue)
    {
        var result = ProjectType.Parse(rawValue);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProjectType.Unsupported");
        result.Error.Message.Should().Contain($"'{rawValue}'");
        result.Error.Message.Should().ContainAll(SupportedNames);
    }

    [Fact]
    public void Parse_required_error_has_the_exact_message()
    {
        var result = ProjectType.Parse(null);

        result.Error.Message.Should().Be(
            "Project type is required. Supported values: webApp, api, console, MAUI.");
    }

    [Fact]
    public void Parse_unsupported_error_has_the_exact_message()
    {
        var result = ProjectType.Parse("blazor");

        result.Error.Message.Should().Be(
            "Unsupported project type 'blazor'. Supported values: webApp, api, console, MAUI.");
    }

    // ---- CanonicalName / ToString / All ----

    [Theory]
    [MemberData(nameof(CanonicalNames))]
    public void CanonicalName_and_ToString_return_the_canonical_name(ProjectType projectType, string expected)
    {
        projectType.CanonicalName.Should().Be(expected);
        projectType.ToString().Should().Be(expected);
    }

    [Fact]
    public void All_contains_exactly_the_four_types_in_canonical_order()
    {
        ProjectType.All.Should().HaveCount(4);
        ProjectType.All.Should().ContainInOrder(
            ProjectType.WebApp, ProjectType.Api, ProjectType.Console, ProjectType.Maui);
    }

    [Fact]
    public void All_is_not_mutable_through_a_list_cast()
    {
        var act = () => ((IList<ProjectType>)ProjectType.All)[0] = ProjectType.Maui;

        act.Should().Throw<NotSupportedException>();
    }

    // ---- Equality: Equals(ProjectType?) ----

    [Fact]
    public void Equals_typed_is_true_for_the_same_instance()
    {
        var projectType = ProjectType.Api;
        var same = projectType;

        projectType.Equals(same).Should().BeTrue();
    }

    [Fact]
    public void Equals_typed_is_true_for_a_parsed_value_and_its_static_instance()
    {
        var parsed = ProjectType.Parse("WEBAPP").Value;

        parsed.Equals(ProjectType.WebApp).Should().BeTrue();
    }

    [Fact]
    public void Equals_typed_is_false_for_different_types()
    {
        ProjectType.Api.Equals(ProjectType.Console).Should().BeFalse();
    }

    [Fact]
    public void Equals_typed_is_false_for_null()
    {
        ProjectType.Api.Equals((ProjectType?)null).Should().BeFalse();
    }

    // ---- Equality: Equals(object?) ----

    [Fact]
    public void Equals_object_is_true_for_an_equal_project_type()
    {
        object other = ProjectType.Maui;

        ProjectType.Maui.Equals(other).Should().BeTrue();
    }

    [Fact]
    public void Equals_object_is_false_for_a_different_project_type()
    {
        object other = ProjectType.Maui;

        ProjectType.WebApp.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void Equals_object_is_false_for_a_non_project_type()
    {
        object other = "api";

        ProjectType.Api.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void Equals_object_is_false_for_null()
    {
        ProjectType.Api.Equals((object?)null).Should().BeFalse();
    }

    // ---- Equality: GetHashCode ----

    [Fact]
    public void GetHashCode_is_consistent_for_equal_values()
    {
        var parsed = ProjectType.Parse("console").Value;

        parsed.GetHashCode().Should().Be(ProjectType.Console.GetHashCode());
        ProjectType.Console.GetHashCode().Should().Be(ProjectType.Console.GetHashCode());
    }

    // ---- Equality: == and != ----

    [Fact]
    public void Equality_operators_hold_for_the_same_instance()
    {
        var left = ProjectType.Console;
        var right = left;

        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
    }

    [Fact]
    public void Equality_operators_hold_for_a_parsed_value_and_its_static_instance()
    {
        var parsed = ProjectType.Parse("maui").Value;

        (parsed == ProjectType.Maui).Should().BeTrue();
        (parsed != ProjectType.Maui).Should().BeFalse();
    }

    [Fact]
    public void Equality_operators_distinguish_different_types()
    {
        var left = ProjectType.WebApp;
        var right = ProjectType.Api;

        (left == right).Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact]
    public void Equality_operators_handle_null_on_the_left()
    {
        ProjectType? left = null;
        var right = ProjectType.Api;

        (left == right).Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact]
    public void Equality_operators_handle_null_on_the_right()
    {
        var left = ProjectType.Api;
        ProjectType? right = null;

        (left == right).Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact]
    public void Equality_operators_treat_two_nulls_as_equal()
    {
        ProjectType? left = null;
        ProjectType? right = null;

        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
    }
}
