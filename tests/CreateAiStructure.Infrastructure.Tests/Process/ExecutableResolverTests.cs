using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.Process;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Infrastructure.Tests.Process;

public class ExecutableResolverTests
{
    private const char Separator = ';';
    private const string PathVariable = "PATH";
    private const string PathExtVariable = "PATHEXT";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "car-resolver-tests");
    private static readonly string DirA = Path.Combine(Root, "a");
    private static readonly string DirB = Path.Combine(Root, "b");
    private static readonly string DirC = Path.Combine(Root, "c");

    private readonly IEnvironmentProvider _environment = Substitute.For<IEnvironmentProvider>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    // ----- helpers -------------------------------------------------------------------------

    private ExecutableResolver CreateWindowsStyle() => new(_environment, _fileSystem, Separator, probePathExtensions: true);

    private ExecutableResolver CreatePosixStyle() => new(_environment, _fileSystem, Separator, probePathExtensions: false);

    private void GivenPath(string? value) => _environment.GetEnvironmentVariable(PathVariable).Returns(value);

    private void GivenPathExt(string? value) => _environment.GetEnvironmentVariable(PathExtVariable).Returns(value);

    private void GivenFileExists(string path) => _fileSystem.FileExists(path).Returns(true);

    private static string JoinPath(params string[] segments) => string.Join(Separator, segments);

    private static string Rooted(string name) => Path.Combine(Path.GetTempPath(), name);

    // ----- constructor guards --------------------------------------------------------------

    [Fact]
    public void Public_ctor_throws_when_environment_is_null()
    {
        var act = () => new ExecutableResolver(null!, _fileSystem);

        act.Should().Throw<ArgumentNullException>().WithParameterName("environment");
    }

    [Fact]
    public void Public_ctor_throws_when_file_system_is_null()
    {
        var act = () => new ExecutableResolver(_environment, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    [Fact]
    public void Internal_ctor_throws_when_environment_is_null()
    {
        var act = () => new ExecutableResolver(null!, _fileSystem, Separator, probePathExtensions: true);

        act.Should().Throw<ArgumentNullException>().WithParameterName("environment");
    }

    [Fact]
    public void Internal_ctor_throws_when_file_system_is_null()
    {
        var act = () => new ExecutableResolver(_environment, null!, Separator, probePathExtensions: false);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fileSystem");
    }

    [Fact]
    public void Public_ctor_constructs_a_working_resolver()
    {
        GivenPath(null);

        var resolver = new ExecutableResolver(_environment, _fileSystem);

        resolver.Should().BeAssignableTo<IExecutableResolver>();
        resolver.Resolve("t3").Should().BeNull();
        _environment.Received(1).GetEnvironmentVariable(PathVariable);
    }

    [Fact]
    public void Public_ctor_resolver_finds_a_tool_using_the_platform_conventions()
    {
        GivenPath(DirA + Path.PathSeparator + DirB);
        GivenPathExt(".EXE");
        var expected = OperatingSystem.IsWindows()
            ? Path.Combine(DirB, "tool.EXE")
            : Path.Combine(DirB, "tool");
        GivenFileExists(expected);

        var resolver = new ExecutableResolver(_environment, _fileSystem);

        resolver.Resolve("tool").Should().Be(expected);
    }

    // ----- IsBareName ----------------------------------------------------------------------

    public static TheoryData<string?> NonBareNames()
    {
        var data = new TheoryData<string?>
        {
            null,
            "",
            "  ",
            "\t",
            Rooted("tool"),
            Rooted("tool.exe"),
            "a/b",
            "a\\b",
            "/tool",
            "\\tool",
            "..",
            "a..b",
            "tool..",
            "..tool",
        };

        if (OperatingSystem.IsWindows())
        {
            // Rooted (drive-relative) without any directory separator.
            data.Add("C:tool");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NonBareNames))]
    public void IsBareName_returns_false_for_non_bare_names(string? toolName)
    {
        ExecutableResolver.IsBareName(toolName).Should().BeFalse();
    }

    [Theory]
    [InlineData("t3")]
    [InlineData("git")]
    [InlineData("git.exe")]
    [InlineData("a.b")]
    [InlineData("my-tool_1")]
    public void IsBareName_returns_true_for_bare_names(string toolName)
    {
        ExecutableResolver.IsBareName(toolName).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonBareNames))]
    public void Resolve_returns_null_for_non_bare_names_without_reading_the_environment_windows_mode(string? toolName)
    {
        GivenPath(DirA);
        GivenFileExists(Path.Combine(DirA, "tool.EXE"));

        var result = CreateWindowsStyle().Resolve(toolName!);

        result.Should().BeNull();
        _environment.ReceivedCalls().Should().BeEmpty();
        _fileSystem.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(NonBareNames))]
    public void Resolve_returns_null_for_non_bare_names_without_reading_the_environment_posix_mode(string? toolName)
    {
        GivenPath(DirA);

        var result = CreatePosixStyle().Resolve(toolName!);

        result.Should().BeNull();
        _environment.ReceivedCalls().Should().BeEmpty();
        _fileSystem.ReceivedCalls().Should().BeEmpty();
    }

    // ----- PATH handling -------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_returns_null_when_PATH_is_null_or_whitespace_windows_mode(string? pathValue)
    {
        GivenPath(pathValue);
        GivenPathExt(".EXE");

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceiveWithAnyArgs().FileExists(default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_returns_null_when_PATH_is_null_or_whitespace_posix_mode(string? pathValue)
    {
        GivenPath(pathValue);

        CreatePosixStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceiveWithAnyArgs().FileExists(default!);
    }

    [Fact]
    public void Resolve_returns_null_when_PATH_contains_only_separators()
    {
        GivenPath(";;;");

        CreatePosixStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceiveWithAnyArgs().FileExists(default!);
    }

    [Fact]
    public void Resolve_respects_directory_order_and_stops_after_first_hit()
    {
        GivenPath(JoinPath(DirA, DirB, DirC));
        GivenFileExists(Path.Combine(DirB, "t3"));
        GivenFileExists(Path.Combine(DirC, "t3"));

        var result = CreatePosixStyle().Resolve("t3");

        result.Should().Be(Path.Combine(DirB, "t3"));
        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3"));
            _fileSystem.FileExists(Path.Combine(DirB, "t3"));
        });
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirC, "t3"));
    }

    [Fact]
    public void Resolve_returns_first_directory_when_several_match()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenFileExists(Path.Combine(DirA, "t3"));
        GivenFileExists(Path.Combine(DirB, "t3"));

        CreatePosixStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3"));

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirB, "t3"));
    }

    [Fact]
    public void Resolve_strips_surrounding_quotes_from_a_segment()
    {
        GivenPath(JoinPath("\"" + DirA + "\""));
        GivenFileExists(Path.Combine(DirA, "t3"));

        CreatePosixStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3"));
    }

    [Fact]
    public void Resolve_trims_before_and_after_stripping_quotes()
    {
        GivenPath(JoinPath("  \" " + DirA + " \"  "));
        GivenFileExists(Path.Combine(DirA, "t3"));

        CreatePosixStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3"));
    }

    [Fact]
    public void Resolve_trims_unquoted_segments()
    {
        GivenPath(JoinPath("   " + DirA + "   "));
        GivenFileExists(Path.Combine(DirA, "t3"));

        CreatePosixStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3"));
    }

    [Fact]
    public void Resolve_strips_only_one_pair_of_quotes()
    {
        // ""dir"" -> "dir" after one strip -> not fully qualified -> skipped.
        GivenPath(JoinPath("\"\"" + DirA + "\"\"", DirB));
        GivenFileExists(Path.Combine(DirA, "t3"));

        CreatePosixStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3"));
        _fileSystem.Received(1).FileExists(Path.Combine(DirB, "t3"));
        _fileSystem.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_does_not_strip_a_leading_quote_without_a_trailing_quote()
    {
        GivenPath(JoinPath("\"" + DirA, DirB));
        GivenFileExists(Path.Combine(DirA, "t3"));

        CreatePosixStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3"));
        _fileSystem.Received(1).FileExists(Path.Combine(DirB, "t3"));
        _fileSystem.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_does_not_strip_a_trailing_quote_without_a_leading_quote()
    {
        GivenPath(JoinPath(DirA + "\""));
        GivenFileExists(Path.Combine(DirA, "t3"));

        CreatePosixStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3"));
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("  \"\"  ")]
    public void Resolve_skips_quote_only_and_quoted_empty_segments(string segment)
    {
        GivenPath(JoinPath(segment, DirA));
        GivenFileExists(Path.Combine(DirA, "t3"));

        CreatePosixStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3"));

        _fileSystem.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_skips_empty_and_whitespace_segments()
    {
        GivenPath(";;" + DirA + ";   ;" + DirB + ";");
        GivenFileExists(Path.Combine(DirB, "t3"));

        CreatePosixStyle().Resolve("t3").Should().Be(Path.Combine(DirB, "t3"));

        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3"));
            _fileSystem.FileExists(Path.Combine(DirB, "t3"));
        });
        _fileSystem.ReceivedCalls().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(".")]
    [InlineData(".\\bin")]
    [InlineData("./bin")]
    [InlineData("bin")]
    [InlineData("..")]
    public void Resolve_never_searches_relative_segments(string relative)
    {
        GivenPath(JoinPath(relative, DirA));
        GivenFileExists(Path.Combine(relative, "t3"));

        CreatePosixStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceive().FileExists(Path.Combine(relative, "t3"));
        _fileSystem.Received(1).FileExists(Path.Combine(DirA, "t3"));
        _fileSystem.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_never_searches_relative_segments_in_pathext_mode()
    {
        GivenPath(JoinPath(".", ".\\bin", "bin", DirA));
        GivenPathExt(".EXE");
        GivenFileExists(Path.Combine(".", "t3.EXE"));
        GivenFileExists(Path.Combine("bin", "t3.EXE"));

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        _fileSystem.Received(1).FileExists(Path.Combine(DirA, "t3.EXE"));
        _fileSystem.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_uses_the_injected_separator()
    {
        var resolver = new ExecutableResolver(_environment, _fileSystem, '|', probePathExtensions: false);
        GivenPath(DirA + "|" + DirB);
        GivenFileExists(Path.Combine(DirB, "t3"));

        resolver.Resolve("t3").Should().Be(Path.Combine(DirB, "t3"));
    }

    // ----- PATHEXT mode --------------------------------------------------------------------

    [Fact]
    public void Pathext_mode_resolves_the_extension_that_exists()
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt(".EXE;.CMD");
        GivenFileExists(Path.Combine(DirA, "t3.CMD"));

        CreateWindowsStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3.CMD"));

        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3.EXE"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.CMD"));
        });
    }

    [Fact]
    public void Pathext_mode_never_probes_the_extensionless_name()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenPathExt(".EXE;.CMD");
        GivenFileExists(Path.Combine(DirA, "t3"));
        GivenFileExists(Path.Combine(DirB, "t3"));

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3"));
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirB, "t3"));
        _fileSystem.ReceivedCalls().Should().HaveCount(4);
    }

    [Fact]
    public void Pathext_mode_normalises_extensions_without_a_dot()
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt("EXE;CMD");
        GivenFileExists(Path.Combine(DirA, "t3.CMD"));

        CreateWindowsStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3.CMD"));

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3CMD"));
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3EXE"));
    }

    [Fact]
    public void Pathext_mode_trims_extensions_and_skips_empty_entries()
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt(" ;  .EXE  ;;   ; CMD ;");

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3.EXE"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.CMD"));
        });
        _fileSystem.ReceivedCalls().Should().HaveCount(2);
    }

    [Fact]
    public void Pathext_mode_probes_case_insensitive_duplicates_once_keeping_the_first_spelling()
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt(".cmd;.CMD;CMD;.Cmd");

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        _fileSystem.Received(1).FileExists(Path.Combine(DirA, "t3.cmd"));
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3.CMD"));
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3.Cmd"));
        _fileSystem.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public void Pathext_mode_dedupes_after_normalising_the_dot()
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt("EXE;.exe;.BAT");

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3.EXE"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.BAT"));
        });
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3.exe"));
        _fileSystem.ReceivedCalls().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(";;")]
    [InlineData("  ")]
    [InlineData(" ; ;  ")]
    public void Pathext_mode_falls_back_to_the_default_extensions(string? pathExt)
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt(pathExt);

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3.COM"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.EXE"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.BAT"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.CMD"));
        });
        _fileSystem.ReceivedCalls().Should().HaveCount(4);
    }

    [Fact]
    public void Pathext_mode_default_extensions_resolve_a_match()
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt(null);
        GivenFileExists(Path.Combine(DirA, "t3.BAT"));

        CreateWindowsStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3.BAT"));

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3.CMD"));
    }

    [Fact]
    public void Pathext_mode_respects_extension_order_within_one_directory()
    {
        GivenPath(JoinPath(DirA));
        GivenPathExt(".CMD;.EXE");
        GivenFileExists(Path.Combine(DirA, "t3.EXE"));
        GivenFileExists(Path.Combine(DirA, "t3.CMD"));

        CreateWindowsStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3.CMD"));

        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3.EXE"));
    }

    [Fact]
    public void Pathext_mode_directory_order_wins_over_extension_order()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenPathExt(".EXE;.CMD");
        GivenFileExists(Path.Combine(DirA, "t3.CMD"));
        GivenFileExists(Path.Combine(DirB, "t3.EXE"));

        CreateWindowsStyle().Resolve("t3").Should().Be(Path.Combine(DirA, "t3.CMD"));

        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3.EXE"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.CMD"));
        });
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirB, "t3.EXE"));
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirB, "t3.CMD"));
    }

    [Fact]
    public void Pathext_mode_probes_directories_in_outer_loop_and_extensions_in_inner_loop()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenPathExt(".EXE;.CMD");

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3.EXE"));
            _fileSystem.FileExists(Path.Combine(DirA, "t3.CMD"));
            _fileSystem.FileExists(Path.Combine(DirB, "t3.EXE"));
            _fileSystem.FileExists(Path.Combine(DirB, "t3.CMD"));
        });
        _fileSystem.ReceivedCalls().Should().HaveCount(4);
    }

    [Fact]
    public void Pathext_mode_probes_a_name_with_an_extension_directly_without_reading_PATHEXT()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenPathExt(".CMD");
        GivenFileExists(Path.Combine(DirB, "git.exe"));

        CreateWindowsStyle().Resolve("git.exe").Should().Be(Path.Combine(DirB, "git.exe"));

        _environment.DidNotReceive().GetEnvironmentVariable(PathExtVariable);
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "git.exe.CMD"));
        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "git.exe"));
            _fileSystem.FileExists(Path.Combine(DirB, "git.exe"));
        });
    }

    [Fact]
    public void Pathext_mode_name_with_an_extension_returns_null_when_absent()
    {
        GivenPath(JoinPath(DirA));

        CreateWindowsStyle().Resolve("git.exe").Should().BeNull();

        _environment.DidNotReceive().GetEnvironmentVariable(PathExtVariable);
        _fileSystem.Received(1).FileExists(Path.Combine(DirA, "git.exe"));
        _fileSystem.ReceivedCalls().Should().HaveCount(1);
    }

    [Fact]
    public void Pathext_mode_returns_null_when_nothing_matches()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenPathExt(".EXE");

        CreateWindowsStyle().Resolve("t3").Should().BeNull();

        _fileSystem.Received(1).FileExists(Path.Combine(DirA, "t3.EXE"));
        _fileSystem.Received(1).FileExists(Path.Combine(DirB, "t3.EXE"));
    }

    // ----- POSIX mode ----------------------------------------------------------------------

    [Fact]
    public void Posix_mode_probes_the_name_directly_without_reading_PATHEXT()
    {
        GivenPath(JoinPath(DirA, DirB));
        GivenPathExt(".EXE;.CMD");
        GivenFileExists(Path.Combine(DirB, "t3"));
        GivenFileExists(Path.Combine(DirA, "t3.EXE"));

        CreatePosixStyle().Resolve("t3").Should().Be(Path.Combine(DirB, "t3"));

        _environment.DidNotReceive().GetEnvironmentVariable(PathExtVariable);
        _fileSystem.DidNotReceive().FileExists(Path.Combine(DirA, "t3.EXE"));
        Received.InOrder(() =>
        {
            _fileSystem.FileExists(Path.Combine(DirA, "t3"));
            _fileSystem.FileExists(Path.Combine(DirB, "t3"));
        });
    }

    [Fact]
    public void Posix_mode_probes_a_name_with_an_extension_directly()
    {
        GivenPath(JoinPath(DirA));
        GivenFileExists(Path.Combine(DirA, "script.sh"));

        CreatePosixStyle().Resolve("script.sh").Should().Be(Path.Combine(DirA, "script.sh"));

        _environment.DidNotReceive().GetEnvironmentVariable(PathExtVariable);
    }

    [Fact]
    public void Posix_mode_returns_null_when_nothing_matches()
    {
        GivenPath(JoinPath(DirA, DirB));

        CreatePosixStyle().Resolve("t3").Should().BeNull();

        _environment.DidNotReceive().GetEnvironmentVariable(PathExtVariable);
        _fileSystem.Received(1).FileExists(Path.Combine(DirA, "t3"));
        _fileSystem.Received(1).FileExists(Path.Combine(DirB, "t3"));
        _fileSystem.ReceivedCalls().Should().HaveCount(2);
    }
}
