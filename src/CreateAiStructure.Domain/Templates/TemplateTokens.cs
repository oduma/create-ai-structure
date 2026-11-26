namespace CreateAiStructure.Domain.Templates;

/// <summary>The placeholder tokens substituted when rendering template files.</summary>
public static class TemplateTokens
{
    /// <summary>The legacy repository-name placeholder.</summary>
    public const string RepoNameLegacy = "{{repo-name}}";

    /// <summary>The repository-name placeholder.</summary>
    public const string RepoName = "{{REPO_NAME}}";

    /// <summary>The repository-slug placeholder (for example <c>owner/repo</c>).</summary>
    public const string RepoSlug = "{{REPO_SLUG}}";

    /// <summary>The placeholder replaced with the list of constitution files.</summary>
    public const string ConstitutionFilesList = "{{list-of-constitution-files}}";

    /// <summary>The placeholder replaced with the list of agent files.</summary>
    public const string AgentsList = "{{list-of-agents}}";
}
