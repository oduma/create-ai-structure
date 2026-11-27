using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Application.Templates;
using CreateAiStructure.Domain.Common;
using CreateAiStructure.Domain.Templates;

namespace CreateAiStructure.Infrastructure.Templates;

/// <summary>
/// Materializes a <see cref="TemplateManifest"/> into a target directory in two phases (Epic #2 FR-4):
/// a pre-flight phase that validates and renders every file in memory with zero writes, then a write phase.
/// </summary>
/// <remarks>
/// All filesystem access goes through <see cref="IFileSystem"/>. Cancellation is never mapped to a failure result:
/// an <see cref="OperationCanceledException"/> always propagates to the caller.
/// </remarks>
public sealed class TemplateMaterializer : ITemplateMaterializer
{
    private const string TargetDirectoryRequiredCode = "TemplateMaterializer.TargetDirectoryRequired";
    private const string InvalidTargetDirectoryCode = "TemplateMaterializer.InvalidTargetDirectory";
    private const string TargetFileAlreadyExistsCode = "TemplateMaterializer.TargetFileAlreadyExists";
    private const string WriteFailedCode = "TemplateMaterializer.WriteFailed";

    /// <summary>
    /// The exception types <see cref="Path.GetFullPath(string)"/> raises for a target directory it cannot normalize.
    /// A type table keeps the catch filter free of per-type conditional branches.
    /// </summary>
    private static readonly Type[] InvalidTargetDirectoryExceptionTypes =
    [
        typeof(ArgumentException),
        typeof(NotSupportedException),
        typeof(PathTooLongException),
    ];

    private readonly ITemplateResourceProvider _resourceProvider;
    private readonly ITemplateRenderer _renderer;
    private readonly IFileSystem _fileSystem;

    /// <summary>Creates a materializer over the given ports.</summary>
    /// <param name="resourceProvider">Reads raw template content.</param>
    /// <param name="renderer">Renders template tokens.</param>
    /// <param name="fileSystem">Performs every filesystem check and write.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public TemplateMaterializer(ITemplateResourceProvider resourceProvider, ITemplateRenderer renderer, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(resourceProvider);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _resourceProvider = resourceProvider;
        _renderer = renderer;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="manifest"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<Result<IReadOnlyList<string>>> MaterializeAsync(
        TemplateManifest manifest,
        TemplateRenderContext context,
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return MaterializeAsync(manifest.Files, context, targetDirectory, cancellationToken);
    }

    /// <summary>
    /// Materializes <paramref name="relativePaths"/> into <paramref name="targetDirectory"/>. The public overload
    /// delegates here with <see cref="TemplateManifest.Files"/>; tests use this seam to supply unsafe paths.
    /// </summary>
    /// <param name="relativePaths">The forward-slash relative template paths, in write order.</param>
    /// <param name="context">The values used to replace the template tokens.</param>
    /// <param name="targetDirectory">The directory the files are written into.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The full paths of the written files in input order, or the first failure encountered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal async Task<Result<IReadOnlyList<string>>> MaterializeAsync(
        IReadOnlyList<string> relativePaths,
        TemplateRenderContext context,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Fail(TargetDirectoryRequiredCode, "A target directory is required.");
        }

        string normalizedRoot;
        try
        {
            normalizedRoot = Path.GetFullPath(targetDirectory);
        }
        catch (Exception ex) when (IsInvalidTargetDirectoryException(ex))
        {
            return Fail(InvalidTargetDirectoryCode, $"Target directory '{targetDirectory}' is invalid: {ex.Message}");
        }

        // Phase A: validate, read and render every file in memory. No directory is created and nothing is written.
        var pendingWrites = new List<(string Destination, string Content)>(relativePaths.Count);
        foreach (var relativePath in relativePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destination = TemplatePathGuard.ResolveDestinationPath(normalizedRoot, relativePath);
            if (destination.IsFailure)
            {
                return Result<IReadOnlyList<string>>.Failure(destination.Error);
            }

            if (_fileSystem.FileExists(destination.Value))
            {
                return Fail(
                    TargetFileAlreadyExistsCode,
                    $"Target file '{destination.Value}' already exists; refusing to overwrite '{relativePath}'.");
            }

            var content = await _resourceProvider.ReadTemplateAsync(relativePath, cancellationToken);
            if (content.IsFailure)
            {
                return Result<IReadOnlyList<string>>.Failure(content.Error);
            }

            pendingWrites.Add((destination.Value, _renderer.Render(content.Value, context)));
        }

        // Phase B: create parent directories and write the rendered files in order.
        var writtenPaths = new string[pendingWrites.Count];
        for (var index = 0; index < pendingWrites.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = pendingWrites[index];
            try
            {
                _fileSystem.CreateDirectory(Path.GetDirectoryName(pending.Destination)!);
                await _fileSystem.WriteAllTextUtf8NoBomAsync(pending.Destination, pending.Content, cancellationToken);
            }
            catch (IOException ex)
            {
                return WriteFailed(pending.Destination, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return WriteFailed(pending.Destination, ex);
            }

            writtenPaths[index] = pending.Destination;
        }

        return Result<IReadOnlyList<string>>.Success(Array.AsReadOnly(writtenPaths));
    }

    private static bool IsInvalidTargetDirectoryException(Exception exception) =>
        Array.Exists(InvalidTargetDirectoryExceptionTypes, type => type.IsInstanceOfType(exception));

    private static Result<IReadOnlyList<string>> WriteFailed(string destination, Exception exception) =>
        Fail(WriteFailedCode, $"Failed to write '{destination}': {exception.Message}");

    private static Result<IReadOnlyList<string>> Fail(string code, string message) =>
        Result<IReadOnlyList<string>>.Failure(new DomainError(code, message));
}
