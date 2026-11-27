using CreateAiStructure.Infrastructure.Templates;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// Specifies <see cref="TemplatePathGuard"/> (Epic #2 FR-4.2, AC-7; Sub-Issue #23): valid relative paths resolve
/// beneath the normalized root; null/blank, backslash, rooted, drive-qualified and <c>..</c> traversal paths are
/// rejected with <c>TemplateMaterializer.UnsafeTemplatePath</c>; and <see cref="TemplatePathGuard.IsContained"/>
/// blocks sibling-prefix escapes. No filesystem access is performed.
/// </summary>
public class TemplatePathGuardTests
{
    private const string UnsafeTemplatePathCode = "TemplateMaterializer.UnsafeTemplatePath";

    private static readonly char Sep = Path.DirectorySeparatorChar;

    private readonly string _tempDirectory = Path.GetFullPath(Path.GetTempPath());

    private readonly string _root;

    public TemplatePathGuardTests()
    {
        _root = Path.GetFullPath(Path.Combine(_tempDirectory, "cas-guard-" + Guid.NewGuid().ToString("N"), "repo"));
    }

    public static TheoryData<string> ValidRelativePaths => new()
    {
        "CLAUDE.md",
        ".gitignore",
        ".claude/agents/TDD-Specialist.md",
        "docs/constitution/tdd.md",
    };

    public static TheoryData<string?> UnsafeRelativePaths => new()
    {
        null,
        "",
        "  ",
        @"a\b.md",
        "/etc/passwd",
        @"C:\evil",
        "C:/evil",
        "C:evil",
        "../escape",
        "a/../../escape",
        "a/..",
        "a\0b",
        ".",
        "./",
    };

    [Theory]
    [MemberData(nameof(ValidRelativePaths))]
    public void ResolveDestinationPath_ValidRelativePath_ReturnsNormalizedCombinedPathUnderRoot(string relativePath)
    {
        var result = TemplatePathGuard.ResolveDestinationPath(_root, relativePath);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Path.GetFullPath(Path.Combine(_root, relativePath)));
        result.Value.Should().StartWith(_root + Sep);
    }

    [Theory]
    [MemberData(nameof(ValidRelativePaths))]
    public void ResolveDestinationPath_RootWithTrailingSeparator_ReturnsNormalizedCombinedPathUnderRoot(string relativePath)
    {
        var rootWithSeparator = _root + Sep;

        var result = TemplatePathGuard.ResolveDestinationPath(rootWithSeparator, relativePath);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Path.GetFullPath(Path.Combine(_root, relativePath)));
        result.Value.Should().StartWith(rootWithSeparator);
    }

    [Theory]
    [MemberData(nameof(ValidRelativePaths))]
    public void ResolveDestinationPath_FilesystemRoot_ReturnsNormalizedCombinedPathUnderRoot(string relativePath)
    {
        var filesystemRoot = Path.GetPathRoot(_tempDirectory)!;

        var result = TemplatePathGuard.ResolveDestinationPath(filesystemRoot, relativePath);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Path.GetFullPath(Path.Combine(filesystemRoot, relativePath)));
        result.Value.Should().StartWith(filesystemRoot);
    }

    [Theory]
    [MemberData(nameof(UnsafeRelativePaths))]
    public void ResolveDestinationPath_UnsafeRelativePath_ReturnsUnsafeTemplatePathFailureNamingThePath(string? relativePath)
    {
        var result = TemplatePathGuard.ResolveDestinationPath(_root, relativePath!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UnsafeTemplatePathCode);
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
        // FluentAssertions cannot assert containment of an empty string; null and "" carry no text to name.
        if (!string.IsNullOrEmpty(relativePath))
        {
            result.Error.Message.Should().Contain(relativePath);
        }
    }

    [Fact]
    public void IsContained_SiblingDirectorySharingRootPrefix_ReturnsFalse()
    {
        var siblingPath = Path.Combine(_root + "-evil", "x");

        TemplatePathGuard.IsContained(_root, siblingPath).Should().BeFalse();
    }

    [Fact]
    public void IsContained_PathBeneathRoot_ReturnsTrue()
    {
        var childPath = Path.Combine(_root, "x");

        TemplatePathGuard.IsContained(_root, childPath).Should().BeTrue();
    }

    [Fact]
    public void IsContained_UnrelatedDirectory_ReturnsFalse()
    {
        var otherPath = Path.Combine(Path.GetDirectoryName(_root)!, "other");

        TemplatePathGuard.IsContained(_root, otherPath).Should().BeFalse();
    }

    [Fact]
    public void IsContained_PathEqualToRoot_ReturnsFalse()
    {
        TemplatePathGuard.IsContained(_root, _root).Should().BeFalse();
    }

    [Fact]
    public void IsContained_RootWithTrailingSeparatorAndPathEqualToRoot_ReturnsFalse()
    {
        var rootWithSeparator = _root + Sep;

        TemplatePathGuard.IsContained(rootWithSeparator, rootWithSeparator).Should().BeFalse();
    }

    [Fact]
    public void IsContained_RootWithTrailingSeparatorAndPathBeneathRoot_ReturnsTrue()
    {
        var childPath = Path.Combine(_root, "x");

        TemplatePathGuard.IsContained(_root + Sep, childPath).Should().BeTrue();
    }

    [Fact]
    public void IsContained_RootWithTrailingSeparatorAndSiblingPath_ReturnsFalse()
    {
        var siblingPath = Path.Combine(_root + "-evil", "x");

        TemplatePathGuard.IsContained(_root + Sep, siblingPath).Should().BeFalse();
    }
}
