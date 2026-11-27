using System.Text;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Infrastructure.Templates;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// Specifies <see cref="EmbeddedTemplateResourceProvider"/> (Epic #2 FR-2, AC-3, AC-4): template indexing,
/// deterministic ordering and immutability of <see cref="EmbeddedTemplateResourceProvider.ListAvailableTemplates"/>,
/// and in-memory reading with BOM stripping, verbatim line endings, typed failures and cancellation.
/// </summary>
public class EmbeddedTemplateResourceProviderTests
{
    private const int ExpectedTemplateFileCount = 26;
    private const string InvalidPathCode = "TemplateResource.InvalidPath";
    private const string NotFoundCode = "TemplateResource.NotFound";
    private const string UnreadableCode = "TemplateResource.Unreadable";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static string TemplatesRoot => Path.Combine(ProjectFile.RepositoryRoot, "templates");

    public static TheoryData<string> OnDiskTemplatePaths()
    {
        var data = new TheoryData<string>();
        foreach (var path in OnDiskRelativePathsSorted())
        {
            data.Add(path);
        }

        return data;
    }

    public static TheoryData<string> ProjectTypeNames()
    {
        var data = new TheoryData<string>();
        foreach (var projectType in ProjectType.All)
        {
            data.Add(projectType.CanonicalName);
        }

        return data;
    }

    // ---------------------------------------------------------------- real assembly

    [Fact]
    public void ListAvailableTemplates_on_the_real_assembly_exactly_matches_the_files_under_templates_in_required_order()
    {
        var expected = OnDiskRelativePathsSorted();
        expected.Should().HaveCount(ExpectedTemplateFileCount);

        var actual = new EmbeddedTemplateResourceProvider().ListAvailableTemplates();

        // Sequence equality with ordinal string equality: exact casing, exact set, exact order.
        actual.Should().Equal(expected);
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void ListAvailableTemplates_on_the_real_assembly_covers_every_manifest_file(string projectTypeName)
    {
        var projectType = ProjectType.Parse(projectTypeName).Value;
        var manifestFiles = TemplateManifest.For(projectType).Files;

        var available = new EmbeddedTemplateResourceProvider().ListAvailableTemplates();

        available.Should().Contain(manifestFiles);
        manifestFiles.Should().OnlyContain(file => available.Contains(file, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(OnDiskTemplatePaths))]
    public async Task ReadTemplateAsync_on_the_real_assembly_returns_the_on_disk_text_without_BOM(string relativePath)
    {
        var expected = ReadOnDiskTextWithoutBom(relativePath);

        var result = await new EmbeddedTemplateResourceProvider().ReadTemplateAsync(relativePath);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Default_constructor_reads_from_the_Infrastructure_assembly()
    {
        var fromDefault = new EmbeddedTemplateResourceProvider().ListAvailableTemplates();
        var fromExplicit = new EmbeddedTemplateResourceProvider(typeof(EmbeddedTemplateResourceProvider).Assembly)
            .ListAvailableTemplates();

        fromDefault.Should().Equal(fromExplicit);
    }

    // ---------------------------------------------------------------- ordering & immutability

    [Fact]
    public void ListAvailableTemplates_sorts_OrdinalIgnoreCase_then_Ordinal()
    {
        var assembly = new FakeResourceAssembly()
            .With("templates/b.md", "b")
            .With("templates/_x.md", "x")
            .With("templates/C.md", "C")
            .With("templates/B.md", "B")
            .With("templates/a.md", "a");

        var actual = new EmbeddedTemplateResourceProvider(assembly).ListAvailableTemplates();

        // Pure Ordinal would give B.md, C.md, _x.md, a.md, b.md.
        actual.Should().Equal("a.md", "B.md", "b.md", "C.md", "_x.md");
    }

    [Fact]
    public void ListAvailableTemplates_cannot_be_mutated_through_ICollection_or_IList_casts()
    {
        var available = new EmbeddedTemplateResourceProvider().ListAvailableTemplates();

        var asCollection = available.Should().BeAssignableTo<ICollection<string>>().Subject;
        asCollection.IsReadOnly.Should().BeTrue();
        asCollection.Invoking(c => c.Add("injected.md")).Should().Throw<NotSupportedException>();
        asCollection.Invoking(c => c.Remove(available.First())).Should().Throw<NotSupportedException>();
        asCollection.Invoking(c => c.Clear()).Should().Throw<NotSupportedException>();

        var asList = available.Should().BeAssignableTo<IList<string>>().Subject;
        asList.Invoking(l => l[0] = "injected.md").Should().Throw<NotSupportedException>();
        asList.Invoking(l => l.Insert(0, "injected.md")).Should().Throw<NotSupportedException>();
        asList.Invoking(l => l.RemoveAt(0)).Should().Throw<NotSupportedException>();

        available.Should().HaveCount(ExpectedTemplateFileCount);
    }

    [Fact]
    public void ListAvailableTemplates_returns_equal_sequences_on_repeated_calls()
    {
        var provider = new EmbeddedTemplateResourceProvider();

        var first = provider.ListAvailableTemplates();
        var second = provider.ListAvailableTemplates();

        second.Should().Equal(first);
    }

    // ---------------------------------------------------------------- indexing (fake assembly)

    [Fact]
    public void Constructor_throws_ArgumentNullException_for_a_null_assembly()
    {
        var act = () => new EmbeddedTemplateResourceProvider(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("resourceAssembly");
    }

    [Fact]
    public void ListAvailableTemplates_is_empty_for_an_assembly_without_resources()
    {
        var provider = new EmbeddedTemplateResourceProvider(new FakeResourceAssembly());

        provider.ListAvailableTemplates().Should().BeEmpty();
    }

    [Fact]
    public void ListAvailableTemplates_ignores_resources_outside_templates_and_the_bare_templates_entry()
    {
        var assembly = new FakeResourceAssembly()
            .With("CreateAiStructure.Infrastructure.Strings.resources", "other")
            .With("Templates/upper-prefix.md", "wrong prefix casing")
            .With("templatesfile.md", "no separator")
            .With("other/templates/nested.md", "prefix not at start")
            .With("templates/", "bare prefix")
            .With("templates/kept.md", "kept");

        var provider = new EmbeddedTemplateResourceProvider(assembly);

        provider.ListAvailableTemplates().Should().Equal("kept.md");
    }

    [Theory]
    [InlineData("upper-prefix.md")]
    [InlineData("Templates/upper-prefix.md")]
    [InlineData("nested.md")]
    [InlineData("other/templates/nested.md")]
    [InlineData("CreateAiStructure.Infrastructure.Strings.resources")]
    public async Task ReadTemplateAsync_cannot_read_ignored_resources(string relativePath)
    {
        var assembly = new FakeResourceAssembly()
            .With("CreateAiStructure.Infrastructure.Strings.resources", "other")
            .With("Templates/upper-prefix.md", "wrong prefix casing")
            .With("other/templates/nested.md", "prefix not at start");

        var result = await new EmbeddedTemplateResourceProvider(assembly).ReadTemplateAsync(relativePath);

        AssertFailure(result, NotFoundCode);
        assembly.RequestedNames.Should().BeEmpty();
    }

    [Fact]
    public async Task Backslash_manifest_names_are_normalised_to_forward_slashes_and_readable()
    {
        var assembly = new FakeResourceAssembly().With(@"templates\sub\file.md", "nested content");
        var provider = new EmbeddedTemplateResourceProvider(assembly);

        provider.ListAvailableTemplates().Should().Equal("sub/file.md");

        var result = await provider.ReadTemplateAsync("sub/file.md");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("nested content");
        assembly.RequestedNames.Should().Equal(@"templates\sub\file.md");
    }

    // ---------------------------------------------------------------- failures

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task ReadTemplateAsync_returns_InvalidPath_for_blank_paths(string? relativePath)
    {
        var assembly = new FakeResourceAssembly().With("templates/CLAUDE.md", "content");

        var result = await new EmbeddedTemplateResourceProvider(assembly).ReadTemplateAsync(relativePath!);

        AssertFailure(result, InvalidPathCode);
        assembly.RequestedNames.Should().BeEmpty();
    }

    [Theory]
    [InlineData("claude.md")]
    [InlineData(@".claude\agents\TDD-Specialist.md")]
    [InlineData("/CLAUDE.md")]
    [InlineData("templates/CLAUDE.md")]
    [InlineData("../CLAUDE.md")]
    [InlineData("missing.md")]
    public async Task ReadTemplateAsync_on_the_real_assembly_returns_NotFound_quoting_the_path(string relativePath)
    {
        var result = await new EmbeddedTemplateResourceProvider().ReadTemplateAsync(relativePath);

        AssertFailure(result, NotFoundCode);
        result.Error.Message.Should().Contain(relativePath);
    }

    [Fact]
    public async Task ReadTemplateAsync_returns_NotFound_for_wrong_casing_on_a_fake_assembly()
    {
        var assembly = new FakeResourceAssembly().With("templates/CLAUDE.md", "content");

        var result = await new EmbeddedTemplateResourceProvider(assembly).ReadTemplateAsync("Claude.md");

        AssertFailure(result, NotFoundCode);
        result.Error.Message.Should().Contain("Claude.md");
        assembly.RequestedNames.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadTemplateAsync_returns_Unreadable_when_the_resource_stream_is_null()
    {
        var assembly = new FakeResourceAssembly().WithNullStream("templates/ghost.md");
        var provider = new EmbeddedTemplateResourceProvider(assembly);

        provider.ListAvailableTemplates().Should().Equal("ghost.md");

        var result = await provider.ReadTemplateAsync("ghost.md");

        AssertFailure(result, UnreadableCode);
        assembly.RequestedNames.Should().Equal("templates/ghost.md");
    }

    // ---------------------------------------------------------------- content decoding

    [Fact]
    public async Task ReadTemplateAsync_strips_a_leading_UTF8_BOM_and_keeps_line_endings()
    {
        var bytes = Utf8Bom.Concat(Encoding.UTF8.GetBytes("first\r\nsecond\nthird")).ToArray();
        var assembly = new FakeResourceAssembly().With("templates/bom.md", bytes);

        var result = await new EmbeddedTemplateResourceProvider(assembly).ReadTemplateAsync("bom.md");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("first\r\nsecond\nthird");
        result.Value[0].Should().NotBe('﻿');
    }

    [Theory]
    [InlineData("line1\r\nline2\r\n")]
    [InlineData("line1\nline2\n")]
    [InlineData("mixed\r\nendings\nkept\r\n")]
    [InlineData("no trailing newline")]
    [InlineData("")]
    [InlineData("unicode: é — ✓\r\n")]
    public async Task ReadTemplateAsync_returns_content_verbatim(string content)
    {
        var assembly = new FakeResourceAssembly().With("templates/file.md", content);

        var result = await new EmbeddedTemplateResourceProvider(assembly).ReadTemplateAsync("file.md");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(content);
    }

    [Fact]
    public async Task ReadTemplateAsync_opens_a_fresh_stream_per_read_and_disposes_it()
    {
        var assembly = new FakeResourceAssembly().With("templates/file.md", "content");
        var provider = new EmbeddedTemplateResourceProvider(assembly);

        var first = await provider.ReadTemplateAsync("file.md");
        var second = await provider.ReadTemplateAsync("file.md");

        first.Value.Should().Be("content");
        second.Value.Should().Be("content");
        assembly.ServedStreams.Should().HaveCount(2);
        assembly.ServedStreams.Should().OnlyContain(stream => !stream.CanRead, "every resource stream must be disposed");
    }

    // ---------------------------------------------------------------- cancellation

    [Theory]
    [InlineData("file.md")]
    [InlineData("missing.md")]
    [InlineData("   ")]
    public async Task ReadTemplateAsync_throws_OperationCanceledException_for_a_pre_cancelled_token(string relativePath)
    {
        var assembly = new FakeResourceAssembly().With("templates/file.md", "content");
        var provider = new EmbeddedTemplateResourceProvider(assembly);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => provider.ReadTemplateAsync(relativePath, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        assembly.RequestedNames.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- helpers

    private static void AssertFailure(Result<string> result, string expectedCode)
    {
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    private static List<string> OnDiskRelativePathsSorted() =>
        Directory.EnumerateFiles(TemplatesRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(TemplatesRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static string ReadOnDiskTextWithoutBom(string relativePath)
    {
        var bytes = File.ReadAllBytes(Path.Combine(TemplatesRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var hasBom = bytes.AsSpan().StartsWith(Utf8Bom);
        var payload = hasBom ? bytes.AsSpan(Utf8Bom.Length) : bytes.AsSpan();
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(payload);
    }
}
