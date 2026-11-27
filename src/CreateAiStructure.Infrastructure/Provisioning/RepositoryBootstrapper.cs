using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Workflow;

namespace CreateAiStructure.Infrastructure.Provisioning;

/// <summary>
/// Creates the GitHub repository with the <c>gh</c> CLI, clones it, normalises its default branch to <c>main</c>
/// and resolves its <c>owner/repo</c> slug; after scaffolding it initializes depviz, creates the workflow labels and
/// commits and pushes the initial structure. Every step fails fast with a structured <see cref="DomainError"/>.
/// </summary>
public sealed class RepositoryBootstrapper : IRepositoryBootstrapper
{
    private const string GhTool = "gh";
    private const string GitTool = "git";
    private const string RepositoryCreateStep = "gh repo create";
    private const string DefaultBranchStep = "git branch -M main";
    private const string RemoteOriginStep = "git remote get-url origin";
    private const string EmptyPlaceholder = "<empty>";
    private const string DepvizTool = "depviz";
    private const string DepvizInitStep = "depviz init";
    private const string GitAddStep = "git add --all";
    private const string GitCommitStep = "git commit";
    private const string GitPushStep = "git push -u origin main";
    private const string InitialCommitMessage = "chore: initialize AI workflow structure";

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
    /// <remarks>
    /// Runs, in order and fail-fast, inside <paramref name="repositoryDirectory"/>: <c>depviz init</c>, one
    /// <c>gh label create</c> per label (input order), <c>git add --all</c>, the initial <c>git commit</c> and
    /// <c>git push -u origin main</c>. The first non-zero exit code stops the pipeline and is reported as a failure.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryDirectory"/> is null, empty or whitespace, or <paramref name="labels"/> contains a
    /// <see langword="null"/> element.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="labels"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<Result> FinalizeRepositoryAsync(
        string repositoryDirectory,
        IReadOnlyList<WorkflowLabelDefinition> labels,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryDirectory);
        ArgumentNullException.ThrowIfNull(labels);
        foreach (var label in labels)
        {
            if (label is null)
            {
                throw new ArgumentException("The label list must not contain null elements.", nameof(labels));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_fileSystem.DirectoryExists(repositoryDirectory))
        {
            return Result.Failure(new DomainError(
                ProvisioningErrorCodes.RepositoryDirectoryNotFound,
                $"The repository directory '{repositoryDirectory}' does not exist."));
        }

        foreach (var step in BuildFinalizeSteps(labels))
        {
            var outcome = await _processRunner.RunAsync(
                new ProcessExecutionRequest(step.FileName, step.Arguments, repositoryDirectory),
                cancellationToken).ConfigureAwait(false);
            if (!outcome.Succeeded)
            {
                return Result.Failure(new DomainError(step.ErrorCode, SubprocessDiagnostics.Describe(step.Description, outcome)));
            }
        }

        return Result.Success();
    }

    private static List<FinalizeStep> BuildFinalizeSteps(IReadOnlyList<WorkflowLabelDefinition> labels)
    {
        List<FinalizeStep> steps = [new(DepvizTool, ["init"], ProvisioningErrorCodes.DepvizInitFailed, DepvizInitStep)];
        foreach (var label in labels)
        {
            steps.Add(new FinalizeStep(
                GhTool,
                ["label", "create", "--color", label.ColorHex, "--description", label.Description, "--force", "--", label.Name],
                ProvisioningErrorCodes.LabelCreateFailed,
                $"gh label create for label '{label.Name}'"));
        }

        steps.Add(new FinalizeStep(GitTool, ["add", "--all"], ProvisioningErrorCodes.GitAddFailed, GitAddStep));
        steps.Add(new FinalizeStep(GitTool, ["commit", "-m", InitialCommitMessage], ProvisioningErrorCodes.GitCommitFailed, GitCommitStep));
        steps.Add(new FinalizeStep(GitTool, ["push", "-u", "origin", "main"], ProvisioningErrorCodes.GitPushFailed, GitPushStep));
        return steps;
    }

    private static Result<RepositoryCloneResult> Fail(string code, string message) =>
        Result<RepositoryCloneResult>.Failure(new DomainError(code, message));

    /// <summary>One external command of the finalization pipeline and how its failure is reported.</summary>
    /// <param name="FileName">The executable to run.</param>
    /// <param name="Arguments">The argument list passed verbatim to the executable.</param>
    /// <param name="ErrorCode">The <see cref="ProvisioningErrorCodes"/> value reported when the command fails.</param>
    /// <param name="Description">The human-readable step name used in the FR-6 diagnostic message.</param>
    private sealed record FinalizeStep(string FileName, IReadOnlyList<string> Arguments, string ErrorCode, string Description);
}
