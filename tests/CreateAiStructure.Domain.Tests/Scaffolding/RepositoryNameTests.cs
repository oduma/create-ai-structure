using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Scaffolding;

public class RepositoryNameTests
{
    private const int MaxLength = 100;

    public static TheoryData<string> ValidNames => new()
    {
        "a",
        "X",
        new string('a', MaxLength),
        "my-repo",
        "my_repo",
        "my.repo",
        "My.Repo-2_x",
        "con-tools",
        "console",
        "COM10",
        "LPT0",
        ".config",
    };

    public static TheoryData<string, string> InvalidNames => new()
    {
        // Required
        { "", "RepositoryName.Required" },

        // TooLong (checked before characters)
        { new string('a', MaxLength + 1), "RepositoryName.TooLong" },
        { new string('a', MaxLength) + "/", "RepositoryName.TooLong" },

        // InvalidCharacters (whitespace is not Required)
        { " ", "RepositoryName.InvalidCharacters" },
        { "a b", "RepositoryName.InvalidCharacters" },
        { "a\tb", "RepositoryName.InvalidCharacters" },
        { "a\u0001b", "RepositoryName.InvalidCharacters" },
        { "a/b", "RepositoryName.InvalidCharacters" },
        { "a\\b", "RepositoryName.InvalidCharacters" },
        { "a:b", "RepositoryName.InvalidCharacters" },
        { "a*b", "RepositoryName.InvalidCharacters" },
        { "a?b", "RepositoryName.InvalidCharacters" },
        { "a\"b", "RepositoryName.InvalidCharacters" },
        { "a<b", "RepositoryName.InvalidCharacters" },
        { "a>b", "RepositoryName.InvalidCharacters" },
        { "a|b", "RepositoryName.InvalidCharacters" },
        { "répo", "RepositoryName.InvalidCharacters" },
        { "-a b", "RepositoryName.InvalidCharacters" },

        // LeadingHyphen (checked before path traversal)
        { "-repo", "RepositoryName.LeadingHyphen" },
        { "-", "RepositoryName.LeadingHyphen" },
        { "-..", "RepositoryName.LeadingHyphen" },

        // PathTraversal (checked before trailing period and .git suffix)
        { ".", "RepositoryName.PathTraversal" },
        { "..", "RepositoryName.PathTraversal" },
        { "a..b", "RepositoryName.PathTraversal" },
        { "a..b.", "RepositoryName.PathTraversal" },
        { "repo..git", "RepositoryName.PathTraversal" },

        // TrailingPeriod (checked before reserved names)
        { "repo.", "RepositoryName.TrailingPeriod" },
        { "con.", "RepositoryName.TrailingPeriod" },

        // GitSuffix (checked before reserved names)
        { "repo.git", "RepositoryName.GitSuffix" },
        { "repo.GIT", "RepositoryName.GitSuffix" },
        { ".git", "RepositoryName.GitSuffix" },
        { "con.git", "RepositoryName.GitSuffix" },

        // ReservedName
        { "CON", "RepositoryName.ReservedName" },
        { "prn", "RepositoryName.ReservedName" },
        { "Aux", "RepositoryName.ReservedName" },
        { "nul.txt", "RepositoryName.ReservedName" },
        { "com1.repo", "RepositoryName.ReservedName" },
    };

    public static TheoryData<string> ReservedDeviceNames
    {
        get
        {
            var data = new TheoryData<string>();
            for (var i = 1; i <= 9; i++)
            {
                data.Add($"COM{i}");
                data.Add($"LPT{i}");
            }

            return data;
        }
    }

    // ---- Create: success ----

    [Theory]
    [MemberData(nameof(ValidNames))]
    public void Create_accepts_valid_names_and_preserves_the_value(string rawValue)
    {
        var result = RepositoryName.Create(rawValue);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(rawValue);
    }

    [Fact]
    public void Create_preserves_original_casing()
    {
        var result = RepositoryName.Create("My.Repo-2_x");

        result.Value.Value.Should().Be("My.Repo-2_x");
    }

    // ---- Create: failure ----

    [Fact]
    public void Create_fails_with_required_error_for_null()
    {
        var result = RepositoryName.Create(null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RepositoryName.Required");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Create_fails_with_the_first_failing_rule_code(string rawValue, string expectedCode)
    {
        var result = RepositoryName.Create(rawValue);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Create_failure_message_names_the_rejected_input(string rawValue, string expectedCode)
    {
        _ = expectedCode;

        var result = RepositoryName.Create(rawValue);

        if (rawValue.Length == 0)
        {
            // Containment of an empty string is meaningless; only require a non-blank message.
            result.Error.Message.Should().NotBeNullOrWhiteSpace();
            return;
        }

        result.Error.Message.Should().Contain(rawValue);
    }

    [Theory]
    [MemberData(nameof(ReservedDeviceNames))]
    public void Create_rejects_every_numbered_device_name(string rawValue)
    {
        var result = RepositoryName.Create(rawValue);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RepositoryName.ReservedName");
        result.Error.Message.Should().Contain(rawValue);
    }

    [Theory]
    [MemberData(nameof(ReservedDeviceNames))]
    public void Create_rejects_numbered_device_names_case_insensitively_and_with_an_extension(string rawValue)
    {
        var result = RepositoryName.Create(rawValue.ToLowerInvariant() + ".repo");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RepositoryName.ReservedName");
    }

    [Fact]
    public void Create_is_deterministic()
    {
        var first = RepositoryName.Create("a b");
        var second = RepositoryName.Create("a b");

        first.Error.Should().Be(second.Error);
    }

    // ---- ToString ----

    [Fact]
    public void ToString_returns_the_value()
    {
        var name = RepositoryName.Create("My.Repo-2_x").Value;

        name.ToString().Should().Be("My.Repo-2_x");
    }

    // ---- Equality ----

    [Fact]
    public void Equals_is_true_for_the_same_value()
    {
        var left = RepositoryName.Create("my-repo").Value;
        var right = RepositoryName.Create("my-repo").Value;

        left.Equals(right).Should().BeTrue();
        left.Equals((object)right).Should().BeTrue();
        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Equals_is_ordinal_and_case_sensitive()
    {
        var left = RepositoryName.Create("my-repo").Value;
        var right = RepositoryName.Create("My-Repo").Value;

        left.Equals(right).Should().BeFalse();
        left.Equals((object)right).Should().BeFalse();
        (left == right).Should().BeFalse();
        (left != right).Should().BeTrue();
    }

    [Fact]
    public void Equals_is_false_for_different_values()
    {
        var left = RepositoryName.Create("repo-a").Value;
        var right = RepositoryName.Create("repo-b").Value;

        left.Equals(right).Should().BeFalse();
        (left == right).Should().BeFalse();
    }

    [Fact]
    public void Equals_typed_is_false_for_null()
    {
        var name = RepositoryName.Create("my-repo").Value;

        name.Equals((RepositoryName?)null).Should().BeFalse();
    }

    [Fact]
    public void Equals_object_is_false_for_null()
    {
        var name = RepositoryName.Create("my-repo").Value;

        name.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void Equals_object_is_false_for_a_non_repository_name()
    {
        var name = RepositoryName.Create("my-repo").Value;

        name.Equals((object)"my-repo").Should().BeFalse();
    }

    [Fact]
    public void Equality_operators_handle_nulls()
    {
        var name = RepositoryName.Create("my-repo").Value;
        RepositoryName? none = null;
        RepositoryName? alsoNone = null;

        (name == none).Should().BeFalse();
        (none == name).Should().BeFalse();
        (none != name).Should().BeTrue();
        (none == alsoNone).Should().BeTrue();
    }
}
