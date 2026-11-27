using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Tooling;
using CreateAiStructure.Infrastructure.Provisioning;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Infrastructure.Tests.Provisioning;

public class ToolValidatorTests
{
    private const char Separator = ';';
    private const string PathVariable = "PATH";
    private const string PathExtVariable = "PATHEXT";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "car-tool-validator-tests");
    private static readonly string DirA = Path.Combine(Root, "a");
    private static readonly string DirB = Path.Combine(Root, "b");

    private readonly IEnvironmentProvider _environment = Substitute.For<IEnvironmentProvider>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    // ----- helpers -------------------------------------------------------------------------

    private ToolValidator CreateWindowsStyle() => new(_environment, _fileSystem, Separator, probePathExtensions: true);

    private ToolValidator CreatePosixStyle() => new(_environment, _fileSystem, Separator, probePathExtensions: false);

    private void GivenPath(string? value) => _environment.GetEnvironmentVariable(PathVariable).Returns(value);

    private void GivenPathExt(string? value) => _environment.GetEnvironmentVariable(PathExtVariable).Returns(value);

    private void GivenFileExists(string path) => _fileSystem.FileExists(path).Returns(true);

    private void GivenEveryFileExists() => _fileSystem.FileExists(Arg.Any<string>()).Returns(true);

    private static string JoinPath(params string[] segments) => string.Join(Separator, segments);

    private static void ShouldBeReadOnly(IReadOnlyList<string> list) =>
        list.Should().BeAssignableTo<ICollection<string>>().Which.IsReadOnly.Should().BeTrue();

    // ----- constructor guards --------------------------------------------------------------

    [Fact]
    public void Public_ctor_throws_when_environment_is_null()
    {
        var act = () => new ToolValidator(null!, _fileSystem);

        act.Should().Throw<ArgumentNullException>().WithParameterName("environment");
    }

    [Fact]
    public void Public_ctor_throws_when_file_system_is_null()
    {
        var act = () => new ToolValidator(_environment, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    [Fact]
    public void Internal_ctor_throws_when_environment_is_null()
    {
        var act = () => new ToolValidator(null!, _fileSystem, Separator, probePathExtensions: true);

        act.Should().Throw<ArgumentNullException>().WithParameterName("environment");
    }

    [Fact]
    public void Internal_ctor_throws_when_file_system_is_null()
    {
        var act = () => new ToolValidator(_environment, null!, Separator, probePathExtensions: false);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    [Fact]
    public async Task Public_ctor_constructs_a_working_validator()
    {
        GivenPath(null);

        var validator = new ToolValidator(_environment, _fileSystem);

        validator.Should().BeAssignableTo<IToolValidator>();
        var result = await validator.ValidateRequiredToolsAsync(["git"]);
        result.IsValid.Should().BeFalse();
        result.MissingTools.Should().Equal("git");
    }

    // ----- argument validation -------------------------------------------------------------

    [Fact]
    public async Task Throws_when_required_tools_is_null()
    {
        var validator = CreateWindowsStyle();

        var act = () => validator.ValidateRequiredToolsAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("requiredTools");
    }

    [Fact]
    public async Task Throws_when_an_entry_is_null_and_validates_before_resolving_anything()
    {
        GivenPath(DirA);
        GivenEveryFileExists();
        var validator = CreateWindowsStyle();

        var act = () => validator.ValidateRequiredToolsAsync(["git", null!]);

        await act.Should().ThrowAsync<ArgumentException>();
        _environment.DidNotReceiveWithAnyArgs().GetEnvironmentVariable(default!);
        _fileSystem.DidNotReceiveWithAnyArgs().FileExists(default!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Throws_when_an_entry_is_empty_or_whitespace(string entry)
    {
        var validator = CreateWindowsStyle();

        var act = () => validator.ValidateRequiredToolsAsync([entry]);

        await act.Should().ThrowAsync<ArgumentException>();
        _environment.DidNotReceiveWithAnyArgs().GetEnvironmentVariable(default!);
    }

    [Fact]
    public async Task Throws_when_a_later_entry_is_whitespace_without_resolving_earlier_entries()
    {
        GivenPath(DirA);
        GivenEveryFileExists();
        var validator = CreateWindowsStyle();

        var act = () => validator.ValidateRequiredToolsAsync(["git", "gh", "   "]);

        await act.Should().ThrowAsync<ArgumentException>();
        _environment.DidNotReceiveWithAnyArgs().GetEnvironmentVariable(default!);
        _fileSystem.DidNotReceiveWithAnyArgs().FileExists(default!);
    }

    [Fact]
    public async Task Throws_when_token_is_already_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var validator = CreateWindowsStyle();

        var act = () => validator.ValidateRequiredToolsAsync(["git"], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _environment.DidNotReceiveWithAnyArgs().GetEnvironmentVariable(default!);
    }

    // ----- resolution ----------------------------------------------------------------------

    [Fact]
    public async Task All_required_tools_present_in_pathext_mode_is_valid()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenPathExt(".COM;.EXE;.BAT;.CMD");
        GivenFileExists(Path.Combine(DirA, "git.EXE"));
        GivenFileExists(Path.Combine(DirB, "gh.EXE"));
        GivenFileExists(Path.Combine(DirA, "depviz.EXE"));
        GivenFileExists(Path.Combine(DirB, "t3.CMD"));
        var validator = CreateWindowsStyle();

        var result = await validator.ValidateRequiredToolsAsync(RequiredTools.All);

        result.IsValid.Should().BeTrue();
        result.FoundTools.Should().Equal("git", "gh", "depviz", "t3");
        result.MissingTools.Should().BeEmpty();
        ShouldBeReadOnly(result.FoundTools);
        ShouldBeReadOnly(result.MissingTools);
    }

    [Fact]
    public async Task Reports_every_missing_tool_in_input_order_without_short_circuiting()
    {
        GivenPath(DirA);
        GivenPathExt(".EXE;.CMD");
        GivenFileExists(Path.Combine(DirA, "gh.EXE"));
        GivenFileExists(Path.Combine(DirA, "t3.CMD"));
        var validator = CreateWindowsStyle();

        var result = await validator.ValidateRequiredToolsAsync(RequiredTools.All);

        result.IsValid.Should().BeFalse();
        result.FoundTools.Should().Equal("gh", "t3");
        result.MissingTools.Should().Equal("git", "depviz");
        ShouldBeReadOnly(result.FoundTools);
        ShouldBeReadOnly(result.MissingTools);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Null_or_empty_path_reports_all_tools_missing(string? path)
    {
        GivenPath(path);
        GivenEveryFileExists();
        var validator = CreateWindowsStyle();

        var result = await validator.ValidateRequiredToolsAsync(RequiredTools.All);

        result.IsValid.Should().BeFalse();
        result.FoundTools.Should().BeEmpty();
        result.MissingTools.Should().Equal("git", "gh", "depviz", "t3");
    }

    [Fact]
    public async Task Duplicates_are_removed_case_insensitively_keeping_first_casing_and_order()
    {
        GivenPath(DirA);
        GivenPathExt(".EXE");
        GivenEveryFileExists();
        var validator = CreateWindowsStyle();

        var result = await validator.ValidateRequiredToolsAsync(["git", "GIT", "gh"]);

        result.IsValid.Should().BeTrue();
        result.FoundTools.Should().Equal("git", "gh");
        result.MissingTools.Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_missing_tools_are_reported_once_with_first_casing()
    {
        GivenPath(DirA);
        var validator = CreatePosixStyle();

        var result = await validator.ValidateRequiredToolsAsync(["Depviz", "depviz", "DEPVIZ"]);

        result.FoundTools.Should().BeEmpty();
        result.MissingTools.Should().Equal("Depviz");
    }

    [Fact]
    public async Task Empty_list_is_valid_with_empty_results()
    {
        var validator = CreateWindowsStyle();

        var result = await validator.ValidateRequiredToolsAsync([]);

        result.IsValid.Should().BeTrue();
        result.FoundTools.Should().BeEmpty();
        result.MissingTools.Should().BeEmpty();
        ShouldBeReadOnly(result.FoundTools);
        ShouldBeReadOnly(result.MissingTools);
    }

    [Fact]
    public async Task Non_bare_names_are_reported_missing()
    {
        GivenPath(DirA);
        GivenPathExt(".EXE");
        GivenEveryFileExists();
        var validator = CreateWindowsStyle();

        var result = await validator.ValidateRequiredToolsAsync(["tools/git", "..\\gh", "a..b"]);

        result.IsValid.Should().BeFalse();
        result.FoundTools.Should().BeEmpty();
        result.MissingTools.Should().Equal("tools/git", "..\\gh", "a..b");
    }

    [Fact]
    public async Task Rooted_names_are_reported_missing()
    {
        var rooted = Path.Combine(DirA, "git");
        GivenPath(DirA);
        GivenEveryFileExists();
        var validator = CreatePosixStyle();

        var result = await validator.ValidateRequiredToolsAsync([rooted]);

        result.FoundTools.Should().BeEmpty();
        result.MissingTools.Should().Equal(rooted);
    }

    [Fact]
    public async Task Posix_mode_resolves_extensionless_names()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenFileExists(Path.Combine(DirA, "git"));
        GivenFileExists(Path.Combine(DirB, "gh"));
        GivenFileExists(Path.Combine(DirB, "t3.cmd"));
        var validator = CreatePosixStyle();

        var result = await validator.ValidateRequiredToolsAsync(RequiredTools.All);

        result.IsValid.Should().BeFalse();
        result.FoundTools.Should().Equal("git", "gh");
        result.MissingTools.Should().Equal("depviz", "t3");
        _environment.DidNotReceive().GetEnvironmentVariable(PathExtVariable);
    }
}
