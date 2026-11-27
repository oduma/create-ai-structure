using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Workflow;

namespace CreateAiStructure.Infrastructure.Provisioning;

/// <summary>
/// Creates the GitHub repository with the <c>gh</c> CLI, clones it, normalises its default branch to <c>main</c>
/// and resolves its <c>owner/repo</c> slug. Every step fails fast with a structured <see cref="DomainError"/>.
/// </summary>
public sealed class RepositoryBootstrapper : IRepositoryBootstrapper
{
    private const string GhTool = "gh";
    private const string GitTool = "git";
    private const string RepositoryCreateStep = "gh repo create";
    private const string DefaultBranchStep = "git branch -M main";
    private const string RemoteOriginStep = "git remote get-url origin";
    private const string EmptyPlaceholder = "<empty>";

    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;

    /// <summary>Initializes a new instance of the <see cref="RepositoryBootstrapper"/> class.</summary>
    /// <param name="processRunner">Runs the external <c>gh</c> and <c>git</c> processes.</param>
    /// <param name="fileSystem">The file system abstraction used for pre-flight and post-clone checks.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public RepositoryBootstrapper(IProcessRunner processRunner, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _processRunner = processRunner;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="repositoryName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="workingDirectory"/> is null, empty or whitespace.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<Result<RepositoryCloneResult>> CreateAndCloneRepositoryAsync(
        RepositoryName repositoryName,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_fileSystem.DirectoryExists(workingDirectory))
        {
            return Fail(
                ProvisioningErrorCodes.WorkingDirectoryNotFound,
                $"The working directory '{workingDirectory}' does not exist.");
        }

        var repositoryDirectory = Path.Combine(workingDirectory, repositoryName.Value);
        if (_fileSystem.DirectoryExists(repositoryDirectory) || _fileSystem.FileExists(repositoryDirectory))
        {
            return Fail(
                ProvisioningErrorCodes.TargetDirectoryAlreadyExists,
                $"The target path '{repositoryDirectory}' already exists.");
        }

        var create = await _processRunner.RunAsync(
            new ProcessExecutionRequest(GhTool, ["repo", "create", "--public", "--clone", "--", repositoryName.Value], workingDirectory),
            cancellationToken).ConfigureAwait(false);
        if (!create.Succeeded)
        {
            return Fail(ProvisioningErrorCodes.RepositoryCreateFailed, SubprocessDiagnostics.Describe(RepositoryCreateStep, create));
        }

        if (!_fileSystem.DirectoryExists(repositoryDirectory))
        {
            return Fail(
                ProvisioningErrorCodes.CloneDirectoryNotFound,
                $"The clone directory '{repositoryDirectory}' was not found after '{RepositoryCreateStep}' succeeded.");
        }

        var branch = await _processRunner.RunAsync(
            new ProcessExecutionRequest(GitTool, ["branch", "-M", "main"], repositoryDirectory),
            cancellationToken).ConfigureAwait(false);
        if (!branch.Succeeded)
        {
            return Fail(ProvisioningErrorCodes.DefaultBranchSetupFailed, SubprocessDiagnostics.Describe(DefaultBranchStep, branch));
        }

        var remote = await _processRunner.RunAsync(
            new ProcessExecutionRequest(GitTool, ["remote", "get-url", "origin"], repositoryDirectory),
            cancellationToken).ConfigureAwait(false);
        if (!remote.Succeeded)
        {
            return Fail(ProvisioningErrorCodes.RemoteOriginLookupFailed, SubprocessDiagnostics.Describe(RemoteOriginStep, remote));
        }

        if (!RemoteOriginSlugParser.TryParse(remote.StandardOutput, out var slug))
        {
            var rawValue = string.IsNullOrWhiteSpace(remote.StandardOutput) ? EmptyPlaceholder : remote.StandardOutput.Trim();
            return Fail(
                ProvisioningErrorCodes.RemoteOriginInvalid,
                $"The 'origin' remote URL '{rawValue}' could not be parsed into an owner/repo slug.");
        }

        return Result<RepositoryCloneResult>.Success(new RepositoryCloneResult(repositoryDirectory, slug));
    }

    /// <inheritdoc />
    /// <exception cref="NotImplementedException">Always; finalization is implemented by a later sub-issue (P3.5).</exception>
    public Task<Result> FinalizeRepositoryAsync(
        string repositoryDirectory,
        IReadOnlyList<WorkflowLabelDefinition> labels,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Repository finalization is not implemented yet.");

    private static Result<RepositoryCloneResult> Fail(string code, string message) =>
        Result<RepositoryCloneResult>.Failure(new DomainError(code, message));
}
