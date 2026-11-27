using System.Text.RegularExpressions;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Infrastructure.Templates;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// Specifies <see cref="TemplateRenderer"/> (Epic #2 FR-3, AC-5; Sub-Issue #22): single-pass, ordinal,
/// non-recursive replacement of the five <see cref="TemplateTokens"/>, verbatim preservation of unknown
/// placeholders and partial braces, line-ending-aware list joining, null guards, and rendering of the real
/// embedded <c>CLAUDE.md</c> and <c>.agents/agents/po/agent.md</c> for every <see cref="ProjectType"/>.
/// </summary>
public class TemplateRendererTests
{
    private const string RepoNameValue = "my-repo";
    private const string ClaudeTemplatePath = "CLAUDE.md";
    private const string PoAgentTemplatePath = ".agents/agents/po/agent.md";
    private const string ConstitutionPrefix = "docs/constitution/";
    private const string AgentPrefix = ".claude/agents/";

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

    // A "\n" that is not preceded by "\r", i.e. a bare LF line ending.
    private static readonly Regex LoneLineFeed = new(@"(?<!\r)\n", RegexOptions.CultureInvariant);

    private readonly TemplateRenderer _renderer = new();

    public static TheoryData<string> ProjectTypeNames()
    {
        var data = new TheoryData<string>();
        foreach (var projectType in ProjectType.All)
        {
            data.Add(projectType.CanonicalName);
        }

        return data;
    }

    public static TheoryData<string> ContentRenderedUnchanged() => new()
    {
        "{{unknown}}",
        "{{REPO_name}}",
        "{{repo_name}}",
        "{{Repo-Name}}",
        "{{ REPO_NAME }}",
        "<epic-number>",
        "{",
        "}",
        "{{",
        "}}",
        "{REPO_NAME}",
        "{{REPO_NAME",
        "REPO_NAME}}",
        "plain text ending in {{",
        "plain text ending in {",
        "",
        "Plain text with no tokens at all.",
        "line one\nline two\n",
        "line one\r\nline two\r\n",
    };

    // ---------------------------------------------------------------- contract

    [Fact]
    public void TemplateRenderer_implements_ITemplateRenderer()
    {
        _renderer.Should().BeAssignableTo<ITemplateRenderer>();
    }

    // ---------------------------------------------------------------- null guards

    [Fact]
    public void Render_throws_ArgumentNullException_when_templateContent_is_null()
    {
        var act = () => _renderer.Render(null!, ContextFor(ProjectType.Api));

        act.Should().Throw<ArgumentNullException>().WithParameterName("templateContent");
    }

    [Fact]
    public void Render_throws_ArgumentNullException_when_context_is_null()
    {
        var act = () => _renderer.Render("{{REPO_NAME}}", null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    // ---------------------------------------------------------------- each token on its own

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_replaces_the_legacy_repo_name_token_with_the_repository_name(string projectTypeName)
    {
        var context = ContextFor(Parse(projectTypeName));

        var rendered = _renderer.Render($"Repo: {TemplateTokens.RepoNameLegacy}.", context);

        rendered.Should().Be($"Repo: {RepoNameValue}.");
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_replaces_the_repo_name_token_with_the_repository_name(string projectTypeName)
    {
        var context = ContextFor(Parse(projectTypeName));

        var rendered = _renderer.Render($"Repo: {TemplateTokens.RepoName}.", context);

        rendered.Should().Be($"Repo: {RepoNameValue}.");
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_replaces_the_repo_slug_token_with_the_repository_slug(string projectTypeName)
    {
        var context = ContextFor(Parse(projectTypeName), "octo-org/my-repo");

        var rendered = _renderer.Render($"Slug: {TemplateTokens.RepoSlug}.", context);

        rendered.Should().Be("Slug: octo-org/my-repo.");
    }

    [Fact]
    public void Render_replaces_the_repo_slug_token_with_the_repository_name_when_no_slug_is_supplied()
    {
        var context = ContextFor(ProjectType.Api);

        var rendered = _renderer.Render(TemplateTokens.RepoSlug, context);

        rendered.Should().Be(RepoNameValue);
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_replaces_the_constitution_files_list_token_with_the_manifest_constitution_files(string projectTypeName)
    {
        var context = ContextFor(Parse(projectTypeName));

        var rendered = _renderer.Render($"Files:\n{TemplateTokens.ConstitutionFilesList}\nEnd", context);

        rendered.Should().Be($"Files:\n{string.Join("\n", context.ConstitutionFiles)}\nEnd");
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_replaces_the_agents_list_token_with_the_manifest_agent_files(string projectTypeName)
    {
        var context = ContextFor(Parse(projectTypeName));

        var rendered = _renderer.Render($"Agents:\n{TemplateTokens.AgentsList}\nEnd", context);

        rendered.Should().Be($"Agents:\n{string.Join("\n", context.AgentFiles)}\nEnd");
    }

    [Fact]
    public void Render_replaces_every_token_kind_in_one_template()
    {
        var context = ContextFor(ProjectType.Console, "octo-org/my-repo");
        var template =
            $"{TemplateTokens.RepoNameLegacy}|{TemplateTokens.RepoName}|{TemplateTokens.RepoSlug}\n" +
            $"{TemplateTokens.ConstitutionFilesList}\n{TemplateTokens.AgentsList}";

        var rendered = _renderer.Render(template, context);

        rendered.Should().Be(
            $"{RepoNameValue}|{RepoNameValue}|octo-org/my-repo\n" +
            $"{string.Join("\n", context.ConstitutionFiles)}\n{string.Join("\n", context.AgentFiles)}");
    }

    // ---------------------------------------------------------------- multiple occurrences

    [Fact]
    public void Render_replaces_every_occurrence_of_the_same_token()
    {
        var context = ContextFor(ProjectType.Api);
        var template =
            $"# {TemplateTokens.RepoNameLegacy}\nline 2\nline 3\nYou are working on {TemplateTokens.RepoNameLegacy}.";

        var rendered = _renderer.Render(template, context);

        rendered.Should().Be($"# {RepoNameValue}\nline 2\nline 3\nYou are working on {RepoNameValue}.");
    }

    [Fact]
    public void Render_replaces_adjacent_occurrences_of_tokens()
    {
        var context = ContextFor(ProjectType.Api, "org/repo");

        var rendered = _renderer.Render(
            $"{TemplateTokens.RepoName}{TemplateTokens.RepoName}{TemplateTokens.RepoSlug}",
            context);

        rendered.Should().Be($"{RepoNameValue}{RepoNameValue}org/repo");
    }

    // ---------------------------------------------------------------- no recursion

    [Fact]
    public void Render_does_not_re_expand_a_slug_equal_to_the_repo_name_token()
    {
        var context = ContextFor(ProjectType.Api, TemplateTokens.RepoName);

        var rendered = _renderer.Render($"Slug: {TemplateTokens.RepoSlug}", context);

        rendered.Should().Be($"Slug: {TemplateTokens.RepoName}");
    }

    [Fact]
    public void Render_does_not_re_expand_a_slug_equal_to_the_agents_list_token()
    {
        var context = ContextFor(ProjectType.WebApp, TemplateTokens.AgentsList);

        var rendered = _renderer.Render($"Slug: {TemplateTokens.RepoSlug}", context);

        rendered.Should().Be($"Slug: {TemplateTokens.AgentsList}");
    }

    [Fact]
    public void Render_does_not_re_expand_a_slug_token_value_while_still_replacing_the_real_tokens_around_it()
    {
        var context = ContextFor(ProjectType.Api, TemplateTokens.RepoNameLegacy);

        var rendered = _renderer.Render(
            $"{TemplateTokens.RepoName} {TemplateTokens.RepoSlug} {TemplateTokens.RepoNameLegacy}",
            context);

        rendered.Should().Be($"{RepoNameValue} {TemplateTokens.RepoNameLegacy} {RepoNameValue}");
    }

    // ---------------------------------------------------------------- preserved verbatim

    [Theory]
    [MemberData(nameof(ContentRenderedUnchanged))]
    public void Render_returns_content_without_known_tokens_unchanged(string templateContent)
    {
        var rendered = _renderer.Render(templateContent, ContextFor(ProjectType.WebApp));

        rendered.Should().Be(templateContent);
    }

    [Fact]
    public void Render_returns_an_empty_string_for_an_empty_template()
    {
        var rendered = _renderer.Render(string.Empty, ContextFor(ProjectType.Maui));

        rendered.Should().BeEmpty();
    }

    [Fact]
    public void Render_keeps_unknown_placeholders_and_partial_braces_around_replaced_tokens()
    {
        var context = ContextFor(ProjectType.Api);
        var template =
            $"{{{{unknown}}}} {{{{REPO_name}}}} <epic-number> {{ }} {TemplateTokens.RepoName} {{{{ }}}} tail {{{{";

        var rendered = _renderer.Render(template, context);

        rendered.Should().Be($"{{{{unknown}}}} {{{{REPO_name}}}} <epic-number> {{ }} {RepoNameValue} {{{{ }}}} tail {{{{");
    }

    [Fact]
    public void Render_replaces_a_token_preceded_by_an_extra_opening_brace_and_keeps_the_stray_braces()
    {
        var context = ContextFor(ProjectType.Api);

        var rendered = _renderer.Render($"{{{TemplateTokens.RepoName}}}", context);

        rendered.Should().Be($"{{{RepoNameValue}}}");
    }

    [Fact]
    public void Render_keeps_the_url_placeholder_of_the_po_agent_template_line()
    {
        var context = ContextFor(ProjectType.Api);

        var rendered = _renderer.Render(
            $"https://github.com/oduma/{TemplateTokens.RepoNameLegacy}/issues/<epic-number>",
            context);

        rendered.Should().Be($"https://github.com/oduma/{RepoNameValue}/issues/<epic-number>");
    }

    // ---------------------------------------------------------------- empty lists

    // Every real TemplateManifest has non-empty ConstitutionFiles and AgentFiles, and TemplateRenderContext can only
    // be built from a TemplateManifest, so a context with empty lists cannot be constructed. The "empty list renders
    // as an empty string" rule is therefore unreachable through the public API: string.Join over an empty list already
    // yields "" and needs no special branch. Instead, this proves that a template containing only scalar tokens (no
    // list tokens) renders without injecting any list content.
    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_of_a_template_with_only_scalar_tokens_injects_no_list_content(string projectTypeName)
    {
        var context = ContextFor(Parse(projectTypeName), "org/repo");
        var template =
            $"{TemplateTokens.RepoNameLegacy}\n{TemplateTokens.RepoName}\n{TemplateTokens.RepoSlug}\n";

        var rendered = _renderer.Render(template, context);

        rendered.Should().Be($"{RepoNameValue}\n{RepoNameValue}\norg/repo\n");
        rendered.Should().NotContain(ConstitutionPrefix).And.NotContain(AgentPrefix);
    }

    // ---------------------------------------------------------------- line endings & ordering

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_joins_lists_with_LF_in_LF_content_in_exact_manifest_order(string projectTypeName)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);
        var context = ContextFor(projectType);
        var template = $"A\n{TemplateTokens.ConstitutionFilesList}\nB\n{TemplateTokens.AgentsList}\nC\n";

        var rendered = _renderer.Render(template, context);

        rendered.Should().Be(
            $"A\n{string.Join("\n", manifest.ConstitutionFiles)}\nB\n{string.Join("\n", manifest.AgentFiles)}\nC\n");
        rendered.Should().NotContain("\r");
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public void Render_joins_lists_with_CRLF_in_CRLF_content_in_exact_manifest_order_without_lone_LF(string projectTypeName)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);
        var context = ContextFor(projectType);
        var template = $"A\r\n{TemplateTokens.ConstitutionFilesList}\r\nB\r\n{TemplateTokens.AgentsList}\r\nC\r\n";

        var rendered = _renderer.Render(template, context);

        rendered.Should().Be(
            $"A\r\n{string.Join("\r\n", manifest.ConstitutionFiles)}\r\nB\r\n{string.Join("\r\n", manifest.AgentFiles)}\r\nC\r\n");
        LoneLineFeed.IsMatch(rendered).Should().BeFalse("CRLF content must not gain bare LF line endings");
    }

    [Fact]
    public void Render_joins_lists_with_CRLF_when_the_only_CRLF_follows_the_list_token()
    {
        var context = ContextFor(ProjectType.Api);

        var rendered = _renderer.Render($"{TemplateTokens.AgentsList}\r\n", context);

        rendered.Should().Be($"{string.Join("\r\n", context.AgentFiles)}\r\n");
        LoneLineFeed.IsMatch(rendered).Should().BeFalse();
    }

    [Fact]
    public void Render_joins_lists_with_LF_when_the_content_has_no_line_breaks()
    {
        var context = ContextFor(ProjectType.Api);

        var rendered = _renderer.Render(TemplateTokens.ConstitutionFilesList, context);

        rendered.Should().Be(string.Join("\n", context.ConstitutionFiles));
    }

    [Fact]
    public void Render_lists_the_webApp_files_in_exact_manifest_order()
    {
        var context = ContextFor(ProjectType.WebApp);

        var constitution = _renderer.Render(TemplateTokens.ConstitutionFilesList, context);
        var agents = _renderer.Render(TemplateTokens.AgentsList, context);

        constitution.Split('\n').Should().Equal(
            "docs/constitution/angular-net-api-architecture.md",
            "docs/constitution/coding-principles.md",
            "docs/constitution/DDD-architecture.md",
            "docs/constitution/tdd.md");
        agents.Split('\n').Should().Equal(
            ".claude/agents/angular-frontend-developer.md",
            ".claude/agents/angular-material-ux.md",
            ".claude/agents/back-end-architect.md",
            ".claude/agents/security-officer.md",
            ".claude/agents/senior-NET-developer.md",
            ".claude/agents/solution-architect.agent.md",
            ".claude/agents/SQL-developer.md",
            ".claude/agents/TDD-Specialist.md");
    }

    [Fact]
    public void Render_lists_the_MAUI_files_in_exact_manifest_order()
    {
        var context = ContextFor(ProjectType.Maui);

        var constitution = _renderer.Render(TemplateTokens.ConstitutionFilesList, context);
        var agents = _renderer.Render(TemplateTokens.AgentsList, context);

        constitution.Split('\n').Should().Equal(
            "docs/constitution/coding-principles.md",
            "docs/constitution/DDD-architecture.md",
            "docs/constitution/maui-architecture.md",
            "docs/constitution/tdd.md");
        agents.Split('\n').Should().Equal(
            ".claude/agents/back-end-architect.md",
            ".claude/agents/MAUI_UX_ENGINEER.md",
            ".claude/agents/security-officer.md",
            ".claude/agents/senior-NET-developer.md",
            ".claude/agents/solution-architect.agent.md",
            ".claude/agents/SQL-developer.md",
            ".claude/agents/TDD-Specialist.md");
    }

    // ---------------------------------------------------------------- real embedded templates

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public async Task Render_of_the_real_CLAUDE_md_resolves_every_token_with_the_exact_per_type_lists(string projectTypeName)
    {
        var projectType = Parse(projectTypeName);
        var manifest = TemplateManifest.For(projectType);
        var raw = await ReadEmbeddedTemplateAsync(ClaudeTemplatePath);
        var newline = raw.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var rendered = _renderer.Render(raw, ContextFor(projectType));

        rendered.Should().NotContain("{{");
        rendered.Should().Contain(RepoNameValue);
        rendered.Should().StartWith($"# {RepoNameValue} ");
        rendered.Should().Contain($"You are working on {RepoNameValue}.");
        rendered.Should().Contain(string.Join(newline, manifest.ConstitutionFiles));
        rendered.Should().Contain(string.Join(newline, manifest.AgentFiles));

        var lines = rendered.Split(newline);
        lines.Where(line => line.StartsWith(ConstitutionPrefix, StringComparison.Ordinal))
            .Should().Equal(manifest.ConstitutionFiles);
        lines.Where(line => line.StartsWith(AgentPrefix, StringComparison.Ordinal))
            .Should().Equal(manifest.AgentFiles);

        if (newline == "\r\n")
        {
            LoneLineFeed.IsMatch(rendered).Should().BeFalse("a CRLF template must not gain bare LF line endings");
        }
        else
        {
            rendered.Should().NotContain("\r");
        }

        AssertProjectSpecificFiles(rendered, projectType);
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public async Task Render_of_the_real_po_agent_resolves_every_token_with_the_repository_name(string projectTypeName)
    {
        var projectType = Parse(projectTypeName);
        var raw = await ReadEmbeddedTemplateAsync(PoAgentTemplatePath);

        var rendered = _renderer.Render(raw, ContextFor(projectType));

        rendered.Should().NotContain("{{");
        rendered.Should().Contain(RepoNameValue);
        rendered.Should().Contain($"https://github.com/oduma/{RepoNameValue}/issues/<epic-number>");
        rendered.Should().NotContain(TemplateTokens.RepoNameLegacy);

        // The PO agent template has no list tokens, so no per-type constitution or agent list is injected.
        foreach (var file in AngularOnlyFiles.Concat(MauiOnlyFiles))
        {
            rendered.Should().NotContain(file);
        }
    }

    [Theory]
    [MemberData(nameof(ProjectTypeNames))]
    public async Task Render_of_the_real_po_agent_only_changes_the_repo_name_token(string projectTypeName)
    {
        var raw = await ReadEmbeddedTemplateAsync(PoAgentTemplatePath);

        var rendered = _renderer.Render(raw, ContextFor(Parse(projectTypeName)));

        rendered.Should().Be(raw.Replace(TemplateTokens.RepoNameLegacy, RepoNameValue, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- helpers

    // Angular files appear only for webApp and MAUI files only for MAUI; neither appears for api/console.
    private static void AssertProjectSpecificFiles(string rendered, ProjectType projectType)
    {
        foreach (var angularFile in AngularOnlyFiles)
        {
            rendered.Contains(angularFile, StringComparison.Ordinal).Should().Be(projectType == ProjectType.WebApp, angularFile);
        }

        foreach (var mauiFile in MauiOnlyFiles)
        {
            rendered.Contains(mauiFile, StringComparison.Ordinal).Should().Be(projectType == ProjectType.Maui, mauiFile);
        }
    }

    private static async Task<string> ReadEmbeddedTemplateAsync(string relativePath)
    {
        var result = await new EmbeddedTemplateResourceProvider().ReadTemplateAsync(relativePath);
        result.IsSuccess.Should().BeTrue($"the real template '{relativePath}' must be embedded");
        return result.Value;
    }

    private static ProjectType Parse(string projectTypeName) => ProjectType.Parse(projectTypeName).Value;

    private static TemplateRenderContext ContextFor(ProjectType projectType, string? repositorySlug = null) =>
        new(RepositoryName.Create(RepoNameValue).Value, TemplateManifest.For(projectType), repositorySlug);
}
