using CreateAiStructure.Application.Launching;
using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Scaffolding;
using CreateAiStructure.Domain.Templates;
using CreateAiStructure.Domain.Tooling;
using CreateAiStructure.Domain.Workflow;

namespace CreateAiStructure.Application.Orchestration;

/// <summary>
/// Runs the end-to-end scaffolding pipeline as a strict, fail-fast sequence: validate the required tools,
/// create and clone the repository, materialize the templates, finalize the repository and launch T3 Code.
/// The first failing step's <see cref="DomainError"/> is returned unchanged and no later step runs.
/// </summary>
public sealed class ScaffoldOrchestrator : IScaffoldOrchestrator
{
    private readonly IToolValidator _toolValidator;
    private readonly IRepositoryBootstrapper _repositoryBootstrapper;
    private readonly ITemplateMaterializer _templateMaterializer;
    private readonly IT3AppLauncher _t3AppLauncher;

    /// <summary>Initializes a new instance of the <see cref="ScaffoldOrchestrator"/> class.</summary>
    /// <param name="toolValidator">Checks that the required command-line tools are installed.</param>
    /// <param name="repositoryBootstrapper">Creates, clones and finalizes the GitHub repository.</param>
    /// <param name="templateMaterializer">Renders and writes the templates into the repository.</param>
    /// <param name="t3AppLauncher">Launches T3 Code for the scaffolded repository.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ScaffoldOrchestrator(
        IToolValidator toolValidator,
        IRepositoryBootstrapper repositoryBootstrapper,
        ITemplateMaterializer templateMaterializer,
        IT3AppLauncher t3AppLauncher)
    {
        ArgumentNullException.ThrowIfNull(toolValidator);
        ArgumentNullException.ThrowIfNull(repositoryBootstrapper);
        ArgumentNullException.ThrowIfNull(templateMaterializer);
        ArgumentNullException.ThrowIfNull(t3AppLauncher);

        _toolValidator = toolValidator;
        _repositoryBootstrapper = repositoryBootstrapper;
        _templateMaterializer = templateMaterializer;
        _t3AppLauncher = t3AppLauncher;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<Result<ScaffoldResult>> ExecuteAsync(
        ScaffoldRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var tools = await _toolValidator.ValidateRequiredToolsAsync(RequiredTools.All, cancellationToken).ConfigureAwait(false);
        if (!tools.IsValid)
        {
            return Result<ScaffoldResult>.Failure(new DomainError(
                OrchestrationErrorCodes.MissingRequiredTools,
                $"Missing required tools on PATH: {string.Join(", ", tools.MissingTools)}. Install them and ensure they are available on PATH before retrying."));
        }

        var clone = await _repositoryBootstrapper.CreateAndCloneRepositoryAsync(
            request.RepositoryName,
            request.WorkingDirectory,
            cancellationToken).ConfigureAwait(false);
        if (clone.IsFailure)
        {
            return Result<ScaffoldResult>.Failure(clone.Error);
        }

        var repositoryDirectory = clone.Value.RepositoryDirectory;
        var manifest = TemplateManifest.For(request.ProjectType);
        var context = new TemplateRenderContext(request.RepositoryName, manifest, clone.Value.RepositorySlug);

        var materialized = await _templateMaterializer.MaterializeAsync(
            manifest,
            context,
            repositoryDirectory,
            cancellationToken).ConfigureAwait(false);
        if (materialized.IsFailure)
        {
            return Result<ScaffoldResult>.Failure(materialized.Error);
        }

        var finalized = await _repositoryBootstrapper.FinalizeRepositoryAsync(
            repositoryDirectory,
            WorkflowLabelCatalog.All,
            cancellationToken).ConfigureAwait(false);
        if (finalized.IsFailure)
        {
            return Result<ScaffoldResult>.Failure(finalized.Error);
        }

        var launch = await _t3AppLauncher.LaunchAsync(repositoryDirectory, cancellationToken).ConfigureAwait(false);
        if (launch.IsFailure)
        {
            return Result<ScaffoldResult>.Failure(launch.Error);
        }

        return Result<ScaffoldResult>.Success(new ScaffoldResult(
            repositoryDirectory,
            materialized.Value,
            launch.Value.StartedDesktopApp,
            launch.Value.ReminderMessage));
    }
}
