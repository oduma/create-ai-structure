using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Domain.Scaffolding;

/// <summary>
/// The validated input for a scaffolding run: what kind of project to scaffold, the repository name,
/// and the working directory in which to create it. Performs no filesystem checks.
/// </summary>
public sealed class ScaffoldRequest
{
    private ScaffoldRequest(ProjectType projectType, RepositoryName repositoryName, string workingDirectory)
    {
        ProjectType = projectType;
        RepositoryName = repositoryName;
        WorkingDirectory = workingDirectory;
    }

    /// <summary>Gets the type of project to scaffold.</summary>
    public ProjectType ProjectType { get; }

    /// <summary>Gets the name of the target repository.</summary>
    public RepositoryName RepositoryName { get; }

    /// <summary>Gets the working directory exactly as supplied (not trimmed or resolved).</summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Validates and creates a scaffold request. Rules are applied in the order project type,
    /// repository name, working directory, and the first failing rule determines the error.
    /// </summary>
    /// <param name="projectType">The project type; must not be null.</param>
    /// <param name="repositoryName">The repository name; must not be null.</param>
    /// <param name="workingDirectory">The working directory; must not be null, empty or whitespace.</param>
    /// <returns>
    /// A successful result with the request, or a failure with one of the codes
    /// <c>ScaffoldRequest.ProjectTypeRequired</c>, <c>ScaffoldRequest.RepositoryNameRequired</c>
    /// or <c>ScaffoldRequest.WorkingDirectoryRequired</c>.
    /// </returns>
    public static Result<ScaffoldRequest> Create(
        ProjectType? projectType, RepositoryName? repositoryName, string? workingDirectory)
    {
        if (projectType is null)
        {
            return Fail("ProjectTypeRequired", "A project type is required.");
        }

        if (repositoryName is null)
        {
            return Fail("RepositoryNameRequired", "A repository name is required.");
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Fail("WorkingDirectoryRequired", "A working directory is required.");
        }

        return Result<ScaffoldRequest>.Success(new ScaffoldRequest(projectType, repositoryName, workingDirectory));
    }

    private static Result<ScaffoldRequest> Fail(string rule, string message) =>
        Result<ScaffoldRequest>.Failure(new DomainError($"ScaffoldRequest.{rule}", message));
}
