using System.Reflection;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// Verifies that every file under the repository's <c>templates/</c> folder is compiled into
/// <c>CreateAiStructure.Infrastructure.dll</c> as an embedded resource whose logical name is
/// <c>templates/&lt;relative/path&gt;</c> (forward slashes, exact casing) and whose bytes match the file on disk.
/// </summary>
public class EmbeddedTemplateCompilationTests
{
    private const string InfrastructureAssemblyName = "CreateAiStructure.Infrastructure";
    private const string ResourcePrefix = "templates/";
    private const int ExpectedTemplateFileCount = 26;

    private static readonly Assembly Infrastructure = Assembly.Load(new AssemblyName(InfrastructureAssemblyName));

    private static string TemplatesRoot => Path.Combine(ProjectFile.RepositoryRoot, "templates");

    public static TheoryData<string> OnDiskTemplateResourceNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in ExpectedResourceNames())
        {
            data.Add(name);
        }

        return data;
    }

    [Fact]
    public void Embedded_template_resource_names_exactly_match_the_files_under_templates()
    {
        var expected = ExpectedResourceNames();

        expected.Should().HaveCount(ExpectedTemplateFileCount);
        // Both lists are ordinal-sorted and string equality is ordinal, so this is an exact, case-sensitive set match.
        EmbeddedTemplateResourceNames().Should().Equal(
            expected,
            "every file under templates/ must be embedded with an ordinal-exact logical name");
    }

    [Fact]
    public void Embedded_template_resource_names_exactly_match_the_union_of_all_manifest_files()
    {
        // Pins the embedded set to the manifest, so stray untracked files on disk (.DS_Store, Thumbs.db, .env, ...)
        // that the on-disk comparison would also accept can never be shipped inside the assembly.
        var expected = ProjectType.All
            .SelectMany(projectType => TemplateManifest.For(projectType).Files)
            .Distinct(StringComparer.Ordinal)
            .Select(path => ResourcePrefix + path)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        expected.Should().HaveCount(ExpectedTemplateFileCount);
        EmbeddedTemplateResourceNames().Should().Equal(
            expected,
            "the embedded templates/ resources must be exactly the union of every project type's manifest files");
    }

    [Theory]
    [InlineData("templates/.gitignore")]
    [InlineData("templates/.workflow-gates.json")]
    [InlineData("templates/.claude/agents/solution-architect.agent.md")]
    [InlineData("templates/.agents/agents/po/agent.md")]
    public void Dotfiles_and_dot_directories_are_embedded(string resourceName)
    {
        EmbeddedTemplateResourceNames().Should().Contain(resourceName);
    }

    [Theory]
    [MemberData(nameof(OnDiskTemplateResourceNames))]
    public void Embedded_resource_bytes_equal_the_file_on_disk(string resourceName)
    {
        var relativePath = resourceName[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var expectedBytes = File.ReadAllBytes(Path.Combine(TemplatesRoot, relativePath));

        using var stream = Infrastructure.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull($"'{resourceName}' must be embedded in {InfrastructureAssemblyName}");

        using var buffer = new MemoryStream();
        stream!.CopyTo(buffer);
        buffer.ToArray().Should().Equal(expectedBytes);
    }

    private static IReadOnlyList<string> EmbeddedTemplateResourceNames() =>
        Infrastructure.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> ExpectedResourceNames() =>
        Directory.EnumerateFiles(TemplatesRoot, "*", SearchOption.AllDirectories)
            .Select(path => ResourcePrefix + Path.GetRelativePath(TemplatesRoot, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
}
