using CreateAiStructure.Domain.Templates;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Templates;

public class TemplateTokensTests
{
    [Fact]
    public void RepoNameLegacy_HasExactValue() => TemplateTokens.RepoNameLegacy.Should().Be("{{repo-name}}");

    [Fact]
    public void RepoName_HasExactValue() => TemplateTokens.RepoName.Should().Be("{{REPO_NAME}}");

    [Fact]
    public void RepoSlug_HasExactValue() => TemplateTokens.RepoSlug.Should().Be("{{REPO_SLUG}}");

    [Fact]
    public void ConstitutionFilesList_HasExactValue() =>
        TemplateTokens.ConstitutionFilesList.Should().Be("{{list-of-constitution-files}}");

    [Fact]
    public void AgentsList_HasExactValue() => TemplateTokens.AgentsList.Should().Be("{{list-of-agents}}");
}
