using System.Collections.ObjectModel;
using CreateAiStructure.Domain.Scaffolding;

namespace CreateAiStructure.Domain.Templates;

/// <summary>
/// The immutable catalog of template files shipped for a given <see cref="Scaffolding.ProjectType"/>.
/// Paths are relative to the <c>templates/</c> directory, use forward slashes and keep the exact
/// casing of the files on disk. Every list is sorted with <see cref="StringComparer.OrdinalIgnoreCase"/>,
/// tie-broken with <see cref="StringComparer.Ordinal"/>.
/// </summary>
public sealed class TemplateManifest
{
    private const string ConstitutionPrefix = "docs/constitution/";
    private const string AgentPrefix = ".claude/agents/";

    private static readonly string[] SharedFiles =
    [
        ".agents/agents/pm/agent.md",
        ".agents/agents/po/agent.md",
        ".agents/agents/qa-verifier/agent.md",
        ".claude/agents/back-end-architect.md",
        ".claude/agents/security-officer.md",
        ".claude/agents/senior-NET-developer.md",
        ".claude/agents/solution-architect.agent.md",
        ".claude/agents/SQL-developer.md",
        ".claude/agents/TDD-Specialist.md",
        ".claude/skills/complete/SKILL.md",
        ".claude/skills/implement/SKILL.md",
        ".claude/skills/plan/SKILL.md",
        "docs/constitution/coding-principles.md",
        "docs/constitution/DDD-architecture.md",
        "docs/constitution/tdd.md",
        "scripts/setup-worktree.ps1",
        ".gitignore",
        ".workflow-gates.json",
        "AGENTS.md",
        "CLAUDE.md",
        "t3.json",
    ];

    // ProjectType's private constructor guarantees this map covers every project type,
    // so no fallback branch is needed.
    private static readonly Dictionary<ProjectType, string[]> ExtraFilesByType = new()
    {
        [ProjectType.WebApp] =
        [
            ".claude/agents/angular-frontend-developer.md",
            ".claude/agents/angular-material-ux.md",
            "docs/constitution/angular-net-api-architecture.md",
        ],
        [ProjectType.Api] = [],
        [ProjectType.Console] = [],
        [ProjectType.Maui] =
        [
            ".claude/agents/MAUI_UX_ENGINEER.md",
            "docs/constitution/maui-architecture.md",
        ],
    };

    private TemplateManifest(ProjectType projectType, string[] files)
    {
        ProjectType = projectType;
        Files = Array.AsReadOnly(files);
        ConstitutionFiles = FilesUnder(files, ConstitutionPrefix);
        AgentFiles = FilesUnder(files, AgentPrefix);
    }

    /// <summary>Gets the project type this manifest describes.</summary>
    public ProjectType ProjectType { get; }

    /// <summary>Gets every template file shipped for the project type.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>Gets the subset of <see cref="Files"/> under <c>docs/constitution/</c>.</summary>
    public IReadOnlyList<string> ConstitutionFiles { get; }

    /// <summary>Gets the subset of <see cref="Files"/> under <c>.claude/agents/</c>.</summary>
    public IReadOnlyList<string> AgentFiles { get; }

    /// <summary>Builds the manifest for <paramref name="projectType"/>.</summary>
    /// <param name="projectType">The project type to describe.</param>
    /// <returns>The template manifest for the project type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projectType"/> is <see langword="null"/>.</exception>
    public static TemplateManifest For(ProjectType projectType)
    {
        ArgumentNullException.ThrowIfNull(projectType);

        var files = Sort(SharedFiles.Concat(ExtraFilesByType[projectType]));

        return new TemplateManifest(projectType, files);
    }

    private static ReadOnlyCollection<string> FilesUnder(string[] files, string prefix) =>
        Array.AsReadOnly(files.Where(file => file.StartsWith(prefix, StringComparison.Ordinal)).ToArray());

    private static string[] Sort(IEnumerable<string> files) =>
        files
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file, StringComparer.Ordinal)
            .ToArray();
}
