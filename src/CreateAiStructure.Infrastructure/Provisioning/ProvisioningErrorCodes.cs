namespace CreateAiStructure.Infrastructure.Provisioning;

/// <summary>
/// Stable, machine-readable <see cref="Domain.Common.DomainError"/> codes produced by repository provisioning (FR-4 and FR-5).
/// </summary>
internal static class ProvisioningErrorCodes
{
    /// <summary>The working directory the repository should be cloned into does not exist.</summary>
    public const string WorkingDirectoryNotFound = "Provisioning.WorkingDirectoryNotFound";

    /// <summary>A directory or file already exists at the target clone path.</summary>
    public const string TargetDirectoryAlreadyExists = "Provisioning.TargetDirectoryAlreadyExists";

    /// <summary><c>gh repo create</c> exited with a non-zero code.</summary>
    public const string RepositoryCreateFailed = "Provisioning.RepositoryCreateFailed";

    /// <summary>The clone directory is missing after <c>gh repo create --clone</c> reported success.</summary>
    public const string CloneDirectoryNotFound = "Provisioning.CloneDirectoryNotFound";

    /// <summary><c>git branch -M main</c> exited with a non-zero code.</summary>
    public const string DefaultBranchSetupFailed = "Provisioning.DefaultBranchSetupFailed";

    /// <summary><c>git remote get-url origin</c> exited with a non-zero code.</summary>
    public const string RemoteOriginLookupFailed = "Provisioning.RemoteOriginLookupFailed";

    /// <summary>The <c>origin</c> remote URL could not be parsed into an <c>owner/repo</c> slug.</summary>
    public const string RemoteOriginInvalid = "Provisioning.RemoteOriginInvalid";

    /// <summary>The repository directory to finalize does not exist.</summary>
    public const string RepositoryDirectoryNotFound = "Provisioning.RepositoryDirectoryNotFound";

    /// <summary><c>depviz init</c> exited with a non-zero code.</summary>
    public const string DepvizInitFailed = "Provisioning.DepvizInitFailed";

    /// <summary><c>gh label create</c> exited with a non-zero code.</summary>
    public const string LabelCreateFailed = "Provisioning.LabelCreateFailed";

    /// <summary><c>git add --all</c> exited with a non-zero code.</summary>
    public const string GitAddFailed = "Provisioning.GitAddFailed";

    /// <summary><c>git commit</c> exited with a non-zero code.</summary>
    public const string GitCommitFailed = "Provisioning.GitCommitFailed";

    /// <summary><c>git push -u origin main</c> exited with a non-zero code.</summary>
    public const string GitPushFailed = "Provisioning.GitPushFailed";
}
