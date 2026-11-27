using System.Text;
using System.Text.RegularExpressions;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Infrastructure.Templates;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// End-to-end specification of <see cref="TemplateMaterializer"/> (Epic #2 AC-8, AC-9, AC-10; Sub-Issue #25):
/// the real embedded resources, the real renderer and a real <see cref="System.IO"/> file system materialize
/// every project type into a fresh temp directory.
/// </summary>
public class TemplateMaterializerDiskTests
{
    private const string RepoNameValue = "disk-materialized-repo";
    private const string ClaudeTemplatePath = "CLAUDE.md";
    private const string PoAgentTemplatePath = ".agents/agents/po/agent.md";
    private const string TargetFileAlreadyExistsCode = "TemplateMaterializer.TargetFileAlreadyExists";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly Regex UnrenderedToken = new(@"\{\{[A-Za-z_-]+\}\}", RegexOptions.CultureInvariant);

    private static readonly string[] AngularOnlyFiles =
    [
        ".claude/agents/angular-frontend-developer.md",
        ".claude/agents/angular-material-ux.md",
        "docs/constitution/angular-net-api-architecture.md",
    ];

    private static readonly string[] MauiOnlyFiles =
    [
        ".claude/agents/MAUI_UX_ENGINEER.md",
        "docs/constitution/maui-architecture.md",
    ];

    private static readonly string[] AllTokens =
    [
        TemplateTokens.RepoNameLegacy,
        TemplateTokens.RepoName,
        TemplateTokens.RepoSlug,
        TemplateTokens.ConstitutionFilesList,
        TemplateTokens.AgentsList,
    ];

    private static string TemplatesRoot => Path.Combine(ProjectFile.RepositoryRoot, "templates");

    public static TheoryData<string, int> ProjectTypesWithFileCounts() => new()
    {
        { "webApp", 24 },
        { "api", 21 },
        { "console", 21 },
        { "MAUI", 23 },
    };

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task Materialize_writes_exactly_the_manifest_file_set_to_disk(string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);
        using var target = new TempDirectory();

        var result = await MaterializeAsync(projectType, target.Path);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        var onDisk = FilesOnDisk(target.Path);
        onDisk.Should().HaveCount(expectedCount);
        onDisk.Should().Equal(manifest.Files.OrderBy(file => file, StringComparer.Ordinal));

        foreach (var angularFile in AngularOnlyFiles)
        {
            onDisk.Contains(angularFile, StringComparer.Ordinal).Should().Be(projectType == ProjectType.WebApp, angularFile);
        }

        foreach (var mauiFile in MauiOnlyFiles)
        {
            onDisk.Contains(mauiFile, StringComparer.Ordinal).Should().Be(projectType == ProjectType.Maui, mauiFile);
        }
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task Materialize_writes_every_file_as_strict_utf8_without_a_bom(string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        using var target = new TempDirectory();
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        var result = await MaterializeAsync(projectType, target.Path);

        result.IsSuccess.Should().BeTrue();
        var onDisk = FilesOnDisk(target.Path);
        onDisk.Should().HaveCount(expectedCount);
        foreach (var file in onDisk)
        {
            var bytes = await File.ReadAllBytesAsync(FullPathOf(target.Path, file));
            bytes.AsSpan().StartsWith(Utf8Bom).Should().BeFalse($"'{file}' must not start with a UTF-8 BOM");
            var decode = () => strictUtf8.GetString(bytes);
            decode.Should().NotThrow($"'{file}' must be valid UTF-8");
        }
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task Materialize_leaves_no_unrendered_tokens_in_the_tokenized_files(string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        using var target = new TempDirectory();

        var result = await MaterializeAsync(projectType, target.Path);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(expectedCount);
        foreach (var file in new[] { ClaudeTemplatePath, PoAgentTemplatePath })
        {
            var content = await File.ReadAllTextAsync(FullPathOf(target.Path, file));
            UnrenderedToken.Matches(content).Select(match => match.Value).Should().BeEmpty(file);
            content.Should().Contain(RepoNameValue, file);
        }
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task Materialize_writes_non_tokenized_files_byte_identical_to_the_templates_source(
        string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);
        using var target = new TempDirectory();

        var result = await MaterializeAsync(projectType, target.Path);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(expectedCount);
        var nonTokenizedCount = 0;
        foreach (var file in manifest.Files)
        {
            var sourceBytes = StripBom(await File.ReadAllBytesAsync(FullPathOf(TemplatesRoot, file)));
            var sourceText = Encoding.UTF8.GetString(sourceBytes);
            if (AllTokens.Any(token => sourceText.Contains(token, StringComparison.Ordinal)))
            {
                continue;
            }

            nonTokenizedCount++;
            var writtenBytes = await File.ReadAllBytesAsync(FullPathOf(target.Path, file));
            writtenBytes.Should().Equal(sourceBytes, $"'{file}' has no tokens and must be byte-identical to its source");
        }

        nonTokenizedCount.Should().Be(expectedCount - 2, "only CLAUDE.md and the PO agent carry tokens");
    }

    [Theory]
    [MemberData(nameof(ProjectTypesWithFileCounts))]
    public async Task Materialize_a_second_time_into_the_same_directory_fails_and_leaves_every_file_unchanged(
        string projectTypeName, int expectedCount)
    {
        var projectType = Parse(projectTypeName);
        using var target = new TempDirectory();

        var first = await MaterializeAsync(projectType, target.Path);
        first.IsSuccess.Should().BeTrue();
        var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in FilesOnDisk(target.Path))
        {
            snapshot[file] = await File.ReadAllBytesAsync(FullPathOf(target.Path, file));
        }

        snapshot.Should().HaveCount(expectedCount);

        var second = await MaterializeAsync(projectType, target.Path);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be(TargetFileAlreadyExistsCode);
        var afterSecond = FilesOnDisk(target.Path);
        afterSecond.Should().Equal(snapshot.Keys.OrderBy(file => file, StringComparer.Ordinal));
        foreach (var (file, bytes) in snapshot)
        {
            (await File.ReadAllBytesAsync(FullPathOf(target.Path, file))).Should().Equal(bytes, file);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Task<Result<IReadOnlyList<string>>> MaterializeAsync(ProjectType projectType, string targetDirectory)
    {
        var manifest = TemplateManifest.For(projectType);
        var context = new TemplateRenderContext(RepositoryName.Create(RepoNameValue).Value, manifest);
        var sut = new TemplateMaterializer(
            new EmbeddedTemplateResourceProvider(), new TemplateRenderer(), new TestUtf8NoBomFileSystem());

        return sut.MaterializeAsync(manifest, context, targetDirectory);
    }

    private static List<string> FilesOnDisk(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();

    private static string FullPathOf(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static byte[] StripBom(byte[] bytes) =>
        bytes.AsSpan().StartsWith(Utf8Bom) ? bytes[Utf8Bom.Length..] : bytes;

    private static ProjectType Parse(string projectTypeName) => ProjectType.Parse(projectTypeName).Value;
}
