using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Tests.Shared;
using FluentAssertions;

namespace CreateAiStructure.Domain.Tests.Templates;

public class TemplateManifestTests
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

    private static readonly string[] AngularFiles =
    [
        ".claude/agents/angular-frontend-developer.md",
        ".claude/agents/angular-material-ux.md",
        "docs/constitution/angular-net-api-architecture.md",
    ];

    private static readonly string[] MauiFiles =
    [
        ".claude/agents/MAUI_UX_ENGINEER.md",
        "docs/constitution/maui-architecture.md",
    ];

    private static readonly string[] AllAngularAndMauiFiles = [.. AngularFiles, .. MauiFiles];

    private static readonly IComparer<string> ManifestOrder = Comparer<string>.Create((left, right) =>
    {
        var result = StringComparer.OrdinalIgnoreCase.Compare(left, right);
        return result != 0 ? result : StringComparer.Ordinal.Compare(left, right);
    });

    public static TheoryData<string> AllProjectTypes => new() { "webApp", "api", "console", "MAUI" };

    public static TheoryData<string, int, int, int> ExpectedCounts => new()
    {
        { "webApp", 24, 4, 8 },
        { "api", 21, 3, 6 },
        { "console", 21, 3, 6 },
        { "MAUI", 23, 4, 7 },
    };

    private static ProjectType TypeNamed(string canonicalName) =>
        ProjectType.All.Single(t => t.CanonicalName == canonicalName);

    private static string[] Sorted(IEnumerable<string> files) => files.Order(ManifestOrder).ToArray();

    private static string[] ExpectedFilesFor(string canonicalName) => canonicalName switch
    {
        "webApp" => Sorted([.. SharedFiles, .. AngularFiles]),
        "MAUI" => Sorted([.. SharedFiles, .. MauiFiles]),
        _ => Sorted(SharedFiles),
    };

    private static IEnumerable<IReadOnlyList<string>> AllListsOf(TemplateManifest manifest) =>
        [manifest.Files, manifest.ConstitutionFiles, manifest.AgentFiles];

    [Fact]
    public void For_NullProjectType_ThrowsArgumentNullException()
    {
        var act = () => TemplateManifest.For(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void For_ExposesRequestedProjectType(string canonicalName)
    {
        var projectType = TypeNamed(canonicalName);

        TemplateManifest.For(projectType).ProjectType.Should().BeSameAs(projectType);
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void Files_EqualExactExpectedListInStrictOrder(string canonicalName)
    {
        var manifest = TemplateManifest.For(TypeNamed(canonicalName));

        manifest.Files.Should().Equal(ExpectedFilesFor(canonicalName));
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void ConstitutionFiles_EqualFilesUnderConstitutionPrefixInStrictOrder(string canonicalName)
    {
        var manifest = TemplateManifest.For(TypeNamed(canonicalName));

        manifest.ConstitutionFiles.Should().Equal(
            ExpectedFilesFor(canonicalName).Where(f => f.StartsWith(ConstitutionPrefix, StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void AgentFiles_EqualFilesUnderAgentPrefixInStrictOrder(string canonicalName)
    {
        var manifest = TemplateManifest.For(TypeNamed(canonicalName));

        manifest.AgentFiles.Should().Equal(
            ExpectedFilesFor(canonicalName).Where(f => f.StartsWith(AgentPrefix, StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(ExpectedCounts))]
    public void Lists_HaveExpectedCounts(string canonicalName, int files, int constitutions, int agents)
    {
        var manifest = TemplateManifest.For(TypeNamed(canonicalName));

        manifest.Files.Should().HaveCount(files);
        manifest.ConstitutionFiles.Should().HaveCount(constitutions);
        manifest.AgentFiles.Should().HaveCount(agents);
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void Lists_AreSortedOrdinalIgnoreCaseThenOrdinal(string canonicalName)
    {
        var manifest = TemplateManifest.For(TypeNamed(canonicalName));

        foreach (var list in AllListsOf(manifest))
        {
            list.Should().Equal(Sorted(list));
        }
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void Files_HaveNoDuplicates(string canonicalName)
    {
        TemplateManifest.For(TypeNamed(canonicalName)).Files.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Console_HasSameFileSetAsApi()
    {
        TemplateManifest.For(ProjectType.Console).Files
            .Should().Equal(TemplateManifest.For(ProjectType.Api).Files);
    }

    [Fact]
    public void WebApp_ContainsAngularFilesAndNoMauiFiles()
    {
        var files = TemplateManifest.For(ProjectType.WebApp).Files;

        files.Should().Contain(AngularFiles);
        files.Should().NotContain(MauiFiles);
    }

    [Fact]
    public void Maui_ContainsMauiFilesAndNoAngularFiles()
    {
        var files = TemplateManifest.For(ProjectType.Maui).Files;

        files.Should().Contain(MauiFiles);
        files.Should().NotContain(AngularFiles);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("console")]
    public void ApiAndConsole_ContainNoAngularOrMauiFiles(string canonicalName)
    {
        TemplateManifest.For(TypeNamed(canonicalName)).Files.Should().NotContain(AllAngularAndMauiFiles);
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void Entries_AreNormalizedRelativeForwardSlashPaths(string canonicalName)
    {
        var manifest = TemplateManifest.For(TypeNamed(canonicalName));

        foreach (var entry in AllListsOf(manifest).SelectMany(list => list))
        {
            entry.Should().NotBeNullOrWhiteSpace();
            entry.Should().NotContain("\\");
            entry.Should().NotStartWith("/");
            entry.Should().NotStartWith("templates/");
        }
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void Lists_CannotBeMutatedThroughCast(string canonicalName)
    {
        var manifest = TemplateManifest.For(TypeNamed(canonicalName));

        foreach (var list in AllListsOf(manifest))
        {
            var mutable = (IList<string>)list;

            FluentActions.Invoking(() => mutable.Add("x")).Should().Throw<NotSupportedException>();
            FluentActions.Invoking(() => mutable[0] = "x").Should().Throw<NotSupportedException>();
            FluentActions.Invoking(() => mutable.Clear()).Should().Throw<NotSupportedException>();
            FluentActions.Invoking(() => mutable.RemoveAt(0)).Should().Throw<NotSupportedException>();
        }
    }

    [Theory]
    [MemberData(nameof(AllProjectTypes))]
    public void Entries_ExistUnderTemplatesDirectoryWithExactCasing(string canonicalName)
    {
        // Drift guard: Windows file systems are case-insensitive, so compare ordinally
        // against the actual enumerated file names rather than using File.Exists.
        var templatesRoot = Path.Combine(ProjectFile.RepositoryRoot, "templates");
        var actualFiles = Directory
            .EnumerateFiles(templatesRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(templatesRoot, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var entry in TemplateManifest.For(TypeNamed(canonicalName)).Files)
        {
            actualFiles.Contains(entry).Should().BeTrue($"'{entry}' must exist under templates/ with exact casing");
        }
    }
}
