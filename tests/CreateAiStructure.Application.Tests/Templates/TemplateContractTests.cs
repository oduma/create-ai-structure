using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using FluentAssertions;
using NSubstitute;

namespace CreateAiStructure.Application.Tests.Templates;

public class TemplateContractTests
{
    private static TemplateManifest Manifest() => TemplateManifest.For(ProjectType.Api);

    private static TemplateRenderContext Context() =>
        new(RepositoryName.Create("repo").Value, Manifest());

    [Fact]
    public async Task Template_resource_provider_is_mockable()
    {
        var provider = Substitute.For<ITemplateResourceProvider>();
        var expected = Result<string>.Success("# {{REPOSITORY_NAME}}");
        IReadOnlyCollection<string> available = ["CLAUDE.md", "AGENTS.md"];
        using var cancellation = new CancellationTokenSource();
        provider.ReadTemplateAsync("CLAUDE.md", cancellation.Token).Returns(Task.FromResult(expected));
        provider.ReadTemplateAsync("AGENTS.md").Returns(Task.FromResult(expected));
        provider.ListAvailableTemplates().Returns(available);

        var withToken = await provider.ReadTemplateAsync("CLAUDE.md", cancellation.Token);
        var withDefault = await provider.ReadTemplateAsync("AGENTS.md");
        var listed = provider.ListAvailableTemplates();

        withToken.Should().BeSameAs(expected);
        withDefault.Should().BeSameAs(expected);
        listed.Should().BeSameAs(available);
        _ = provider.Received(1).ReadTemplateAsync("CLAUDE.md", cancellation.Token);
        _ = provider.Received(1).ReadTemplateAsync("AGENTS.md");
        provider.Received(1).ListAvailableTemplates();
    }

    [Fact]
    public void Template_renderer_is_mockable()
    {
        var renderer = Substitute.For<ITemplateRenderer>();
        var context = Context();
        renderer.Render("template", context).Returns("rendered");

        var actual = renderer.Render("template", context);

        actual.Should().Be("rendered");
        renderer.Received(1).Render("template", context);
    }

    [Fact]
    public async Task Template_materializer_is_mockable()
    {
        var materializer = Substitute.For<ITemplateMaterializer>();
        var manifest = Manifest();
        var context = Context();
        IReadOnlyList<string> written = [@"C:\work\repo\CLAUDE.md"];
        var expected = Result<IReadOnlyList<string>>.Success(written);
        using var cancellation = new CancellationTokenSource();
        materializer.MaterializeAsync(manifest, context, @"C:\work\repo", cancellation.Token)
            .Returns(Task.FromResult(expected));
        materializer.MaterializeAsync(manifest, context, @"C:\work\other")
            .Returns(Task.FromResult(expected));

        var withToken = await materializer.MaterializeAsync(manifest, context, @"C:\work\repo", cancellation.Token);
        var withDefault = await materializer.MaterializeAsync(manifest, context, @"C:\work\other");

        withToken.Should().BeSameAs(expected);
        withDefault.Should().BeSameAs(expected);
        _ = materializer.Received(1).MaterializeAsync(manifest, context, @"C:\work\repo", cancellation.Token);
        _ = materializer.Received(1).MaterializeAsync(manifest, context, @"C:\work\other");
    }
}
