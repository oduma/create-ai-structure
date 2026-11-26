using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Templates;

public class TemplateRenderContextTests
{
    private static RepositoryName Name(string value) => RepositoryName.Create(value).Value;

    private static TemplateManifest Manifest() => TemplateManifest.For(ProjectType.WebApp);

    [Fact]
    public void Constructor_NullRepositoryName_ThrowsArgumentNullException()
    {
        var act = () => new TemplateRenderContext(null!, Manifest());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullManifest_ThrowsArgumentNullException()
    {
        var act = () => new TemplateRenderContext(Name("my-repo"), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RepositoryName_IsTheSuppliedName()
    {
        var name = Name("my-repo");

        new TemplateRenderContext(name, Manifest()).RepositoryName.Should().BeSameAs(name);
    }

    [Fact]
    public void RepositorySlug_WhenOmitted_DefaultsToRepositoryNameValue()
    {
        new TemplateRenderContext(Name("my-repo"), Manifest()).RepositorySlug.Should().Be("my-repo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RepositorySlug_WhenNullOrWhitespace_DefaultsToRepositoryNameValue(string? slug)
    {
        new TemplateRenderContext(Name("my-repo"), Manifest(), slug).RepositorySlug.Should().Be("my-repo");
    }

    [Fact]
    public void RepositorySlug_WhenSupplied_IsPreserved()
    {
        new TemplateRenderContext(Name("my-repo"), Manifest(), "owner/repo").RepositorySlug.Should().Be("owner/repo");
    }

    [Theory]
    [InlineData("webApp")]
    [InlineData("api")]
    [InlineData("console")]
    [InlineData("MAUI")]
    public void FileLists_MatchManifest(string canonicalName)
    {
        var manifest = TemplateManifest.For(ProjectType.All.Single(t => t.CanonicalName == canonicalName));

        var context = new TemplateRenderContext(Name("my-repo"), manifest);

        context.ConstitutionFiles.Should().Equal(manifest.ConstitutionFiles);
        context.AgentFiles.Should().Equal(manifest.AgentFiles);
    }
}
