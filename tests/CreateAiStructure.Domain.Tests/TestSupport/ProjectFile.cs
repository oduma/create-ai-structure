using System.Xml.Linq;

namespace CreateAiStructure.Tests.Shared;

/// <summary>
/// Read-only view over an SDK-style <c>.csproj</c> file in this repository, used by the
/// architecture guard tests to verify the project reference graph declaratively.
/// </summary>
internal sealed class ProjectFile
{
    private const string SolutionFileName = "CreateAiStructure.slnx";

    private readonly XDocument _document;

    private ProjectFile(XDocument document) => _document = document;

    /// <summary>Absolute path of the repository root (the folder containing the solution file).</summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Loads <c>{folder}/{projectName}/{projectName}.csproj</c> relative to the repository root.</summary>
    public static ProjectFile Load(string folder, string projectName) =>
        new(XDocument.Load(Path.Combine(RepositoryRoot, folder, projectName, projectName + ".csproj")));

    /// <summary>Referenced project names (csproj file names without extension).</summary>
    public IReadOnlyList<string> ProjectReferences =>
        IncludesOf("ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToList();

    /// <summary>NuGet package ids referenced by the project.</summary>
    public IReadOnlyList<string> PackageReferences => IncludesOf("PackageReference").ToList();

    /// <summary>Value of the first MSBuild property with the given name, or <c>null</c>.</summary>
    public string? Property(string name) =>
        _document.Descendants("PropertyGroup").Elements(name).Select(e => e.Value.Trim()).FirstOrDefault();

    private IEnumerable<string> IncludesOf(string itemType) =>
        _document.Descendants(itemType)
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>();

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate '{SolutionFileName}' above '{AppContext.BaseDirectory}'.");
    }
}
