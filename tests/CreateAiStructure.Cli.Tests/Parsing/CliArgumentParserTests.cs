using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Cli.Parsing;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Cli.Tests.Parsing;

public class CliArgumentParserTests
{
    private const string WorkingDirectory = @"C:\work";

    private static readonly string[] SupportedNames = ["webApp", "api", "console", "MAUI"];

    private readonly IEnvironmentProvider _environment = Substitute.For<IEnvironmentProvider>();

    public CliArgumentParserTests()
    {
        _environment.GetCurrentDirectory().Returns(WorkingDirectory);
    }

    private CliArgumentParser CreateParser() => new(_environment);

    private Result<CliCommand> Parse(params string[] args) => CreateParser().Parse(args);

    private static void ShouldFail(Result<CliCommand> result, string code, params string[] messageFragments)
    {
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(code);
        if (messageFragments.Length > 0)
        {
            result.Error.Message.Should().ContainAll(messageFragments);
        }
    }

    // ---- Constructor & guards ----

    [Fact]
    public void Constructor_throws_when_environment_is_null()
    {
        var act = () => new CliArgumentParser(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("environment");
    }

    [Fact]
    public void Parse_throws_when_args_is_null()
    {
        var act = () => CreateParser().Parse(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("args");
    }

    // ---- Valid scaffold forms ----

    public static TheoryData<string[], ProjectType, string> ValidInvocations => new()
    {
        // Space form, all four types in mixed casing.
        { ["--type", "WEBAPP", "--repo", "my-app"], ProjectType.WebApp, "my-app" },
        { ["--type", "Api", "--repo", "my-api"], ProjectType.Api, "my-api" },
        { ["--type", "CONSOLE", "--repo", "My_Tool.1"], ProjectType.Console, "My_Tool.1" },
        { ["--type", "maui", "--repo", "mobile"], ProjectType.Maui, "mobile" },

        // Equals form.
        { ["--type=WEBAPP", "--repo=my-app"], ProjectType.WebApp, "my-app" },
        { ["--type=Api", "--repo=my-api"], ProjectType.Api, "my-api" },
        { ["--type=CONSOLE", "--repo=tool"], ProjectType.Console, "tool" },
        { ["--type=maui", "--repo=mobile"], ProjectType.Maui, "mobile" },

        // Reversed order.
        { ["--repo", "my-app", "--type", "WEBAPP"], ProjectType.WebApp, "my-app" },
        { ["--repo=my-api", "--type=Api"], ProjectType.Api, "my-api" },

        // Mixed forms.
        { ["--type", "CONSOLE", "--repo=tool"], ProjectType.Console, "tool" },
        { ["--type=maui", "--repo", "mobile"], ProjectType.Maui, "mobile" },
        { ["--repo=mobile", "--type", "maui"], ProjectType.Maui, "mobile" },
        { ["--repo", "web", "--type=WEBAPP"], ProjectType.WebApp, "web" },
    };

    [Theory]
    [MemberData(nameof(ValidInvocations))]
    public void Parse_returns_scaffold_command_for_valid_invocations(
        string[] args, ProjectType expectedType, string expectedRepo)
    {
        var result = CreateParser().Parse(args);

        result.IsSuccess.Should().BeTrue();
        var command = result.Value.Should().BeOfType<ScaffoldCommand>().Subject;
        command.Request.ProjectType.Should().BeSameAs(expectedType);
        command.Request.RepositoryName.Value.Should().Be(expectedRepo);
        command.Request.WorkingDirectory.Should().Be(WorkingDirectory);
    }

    [Fact]
    public void Parse_reads_working_directory_from_environment_provider()
    {
        _environment.GetCurrentDirectory().Returns("/home/user/src");

        var result = Parse("--type", "api", "--repo", "svc");

        result.Value.Should().BeOfType<ScaffoldCommand>()
            .Which.Request.WorkingDirectory.Should().Be("/home/user/src");
        _environment.Received(1).GetCurrentDirectory();
    }

    [Fact]
    public void Parse_does_not_query_environment_when_domain_validation_fails_earlier()
    {
        Parse("--type", "nope", "--repo", "svc");

        _environment.DidNotReceive().GetCurrentDirectory();
    }

    [Fact]
    public void Equals_form_splits_on_the_first_equals_sign_only()
    {
        // The value "a=b" reaches the Domain intact and is rejected there ('=' is not an allowed character).
        ShouldFail(Parse("--type=api", "--repo=a=b"), "RepositoryName.InvalidCharacters", "'a=b'");
    }

    [Fact]
    public void Space_form_takes_a_next_token_starting_with_double_dash_as_the_value()
    {
        // "--repo" is consumed as the --type value, then "x" is the --repo value via the real flag.
        ShouldFail(
            Parse("--type", "--repo", "--repo", "x"),
            "ProjectType.Unsupported",
            "'--repo'");
    }

    [Fact]
    public void Space_form_passes_a_double_dash_repo_value_to_domain_validation()
    {
        ShouldFail(Parse("--type", "api", "--repo", "--x"), "RepositoryName.LeadingHyphen", "'--x'");
    }

    // ---- Help & version ----

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--type", "api", "--repo", "svc", "--help")]
    [InlineData("-h", "--type")]
    [InlineData("--foo", "extra", "-h")]
    [InlineData("--type", "nope", "--repo", "../x", "--help")]
    [InlineData("--help", "--version")]
    [InlineData("-v", "-h")]
    [InlineData("--version", "--help")]
    public void Parse_returns_help_when_any_help_flag_is_present(params string[] args)
    {
        var result = Parse(args);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<HelpCommand>();
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("--type", "api", "--repo", "svc", "--version")]
    [InlineData("-v", "--repo")]
    [InlineData("--foo", "extra", "-v")]
    [InlineData("--type", "nope", "--type", "x", "--version")]
    public void Parse_returns_version_when_a_version_flag_is_present_without_help(params string[] args)
    {
        var result = Parse(args);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<VersionCommand>();
    }

    [Fact]
    public void Parse_does_not_query_environment_for_help_or_version()
    {
        Parse("--help");
        Parse("--version");

        _environment.DidNotReceive().GetCurrentDirectory();
    }

    // ---- CLI errors ----

    [Fact]
    public void Parse_fails_with_missing_required_options_for_empty_args()
    {
        var result = Parse();

        ShouldFail(result, CliErrorCodes.MissingRequiredOptions, "--type", "--repo");
        result.Error.Message.Should().Be("The --type and --repo options are required.");
    }

    [Theory]
    [InlineData("--repo", "svc")]
    [InlineData("--repo=svc")]
    public void Parse_fails_with_missing_type_when_only_repo_is_supplied(params string[] args)
    {
        var result = Parse(args);

        ShouldFail(result, CliErrorCodes.MissingRequiredOption, "--type");
        result.Error.Message.Should().Be("The --type option is required.");
    }

    [Theory]
    [InlineData("--type", "api")]
    [InlineData("--type=api")]
    public void Parse_fails_with_missing_repo_when_only_type_is_supplied(params string[] args)
    {
        var result = Parse(args);

        ShouldFail(result, CliErrorCodes.MissingRequiredOption, "--repo");
        result.Error.Message.Should().Be("The --repo option is required.");
    }

    public static TheoryData<string[], string> MissingValueInvocations => new()
    {
        { ["--type"], "--type" },
        { ["--repo", "svc", "--type"], "--type" },
        { ["--repo"], "--repo" },
        { ["--type", "api", "--repo"], "--repo" },
        { ["--type="], "--type" },
        { ["--repo=", "--type", "api"], "--repo" },
        { ["--type", "api", "--repo="], "--repo" },
        // The value is read before the duplicate check, so a trailing repeated flag reports the missing value.
        { ["--type", "a", "--type"], "--type" },
    };

    [Theory]
    [MemberData(nameof(MissingValueInvocations))]
    public void Parse_fails_with_missing_option_value_naming_the_flag(string[] args, string flag)
    {
        var result = Parse(args);

        ShouldFail(result, CliErrorCodes.MissingOptionValue, flag);
        result.Error.Message.Should().Be($"The {flag} option requires a value.");
    }

    public static TheoryData<string[], string> DuplicateInvocations => new()
    {
        { ["--type", "api", "--type", "console", "--repo", "svc"], "--type" },
        { ["--type", "api", "--type=console", "--repo", "svc"], "--type" },
        { ["--type=api", "--repo", "svc", "--type", "api"], "--type" },
        { ["--type", "api", "--repo", "a", "--repo", "b"], "--repo" },
        { ["--repo=a", "--repo=b", "--type", "api"], "--repo" },
    };

    [Theory]
    [MemberData(nameof(DuplicateInvocations))]
    public void Parse_fails_with_duplicate_option_naming_the_flag(string[] args, string flag)
    {
        var result = Parse(args);

        ShouldFail(result, CliErrorCodes.DuplicateOption, flag);
        result.Error.Message.Should().Be($"The {flag} option was specified more than once.");
    }

    [Theory]
    [InlineData("--foo")]
    [InlineData("-x")]
    [InlineData("extra")]
    [InlineData("--foo=x")]
    [InlineData("=x")]
    [InlineData("--HELP")]
    [InlineData("-H")]
    [InlineData("--Version")]
    [InlineData("-V")]
    [InlineData("--Type")]
    [InlineData("--REPO")]
    [InlineData("--Type=api")]
    public void Parse_fails_with_unknown_argument_naming_the_token_verbatim(string token)
    {
        var result = Parse("--type", "api", token, "--repo", "svc");

        ShouldFail(result, CliErrorCodes.UnknownArgument, $"'{token}'");
        result.Error.Message.Should().Be($"Unknown argument '{token}'.");
    }

    [Fact]
    public void Parse_fails_on_the_first_unknown_argument_left_to_right()
    {
        ShouldFail(Parse("extra", "--foo"), CliErrorCodes.UnknownArgument, "'extra'");
    }

    // ---- Domain error passthrough ----

    [Theory]
    [InlineData("--type", "desktop", "--repo", "svc")]
    [InlineData("--type=desktop", "--repo=svc")]
    public void Parse_passes_through_unsupported_project_type_error(params string[] args)
    {
        var result = Parse(args);

        ShouldFail(result, "ProjectType.Unsupported", "'desktop'");
        result.Error.Message.Should().ContainAll(SupportedNames);
        result.Error.Should().Be(ProjectType.Parse("desktop").Error);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Parse_passes_through_required_project_type_error_for_blank_type(string blankType)
    {
        var result = Parse("--type", blankType, "--repo", "svc");

        ShouldFail(result, "ProjectType.Required");
        result.Error.Should().Be(ProjectType.Parse(blankType).Error);
    }

    [Theory]
    [InlineData("../x", "RepositoryName.InvalidCharacters")]
    [InlineData("-x", "RepositoryName.LeadingHyphen")]
    [InlineData("CON", "RepositoryName.ReservedName")]
    [InlineData("a/b", "RepositoryName.InvalidCharacters")]
    [InlineData("x.git", "RepositoryName.GitSuffix")]
    [InlineData("..x", "RepositoryName.PathTraversal")]
    public void Parse_passes_through_repository_name_errors(string repo, string expectedCode)
    {
        var spaceForm = Parse("--type", "api", "--repo", repo);
        var equalsForm = Parse("--type=api", $"--repo={repo}");

        ShouldFail(spaceForm, expectedCode, repo);
        spaceForm.Error.Should().Be(RepositoryName.Create(repo).Error);
        equalsForm.Error.Should().Be(spaceForm.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_passes_through_working_directory_error_for_blank_current_directory(string cwd)
    {
        _environment.GetCurrentDirectory().Returns(cwd);

        var result = Parse("--type", "api", "--repo", "svc");

        ShouldFail(result, "ScaffoldRequest.WorkingDirectoryRequired");
        result.Error.Should().Be(
            ScaffoldRequest.Create(ProjectType.Api, RepositoryName.Create("svc").Value, cwd).Error);
    }

    [Fact]
    public void Parse_validates_project_type_before_repository_name()
    {
        ShouldFail(Parse("--type", "nope", "--repo", "../x"), "ProjectType.Unsupported");
    }
}
