using CreateAiStructure.Application.Provisioning;
using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.Process;

namespace CreateAiStructure.Infrastructure.Provisioning;

/// <summary>
/// Validates that every required external tool resolves to an executable on <c>PATH</c>.
/// Every tool is checked (no short-circuit) so that all missing tools can be reported at once.
/// </summary>
public sealed class ToolValidator : IToolValidator
{
    private readonly ExecutableResolver _resolver;

    /// <summary>Initializes a new instance using the current platform's path separator and extension conventions.</summary>
    /// <param name="environment">The environment variable provider.</param>
    /// <param name="fileSystem">The file system abstraction used to probe candidates.</param>
    public ToolValidator(IEnvironmentProvider environment, IFileSystem fileSystem)
        : this(environment, fileSystem, Path.PathSeparator, OperatingSystem.IsWindows())
    {
    }

    /// <summary>Initializes a new instance with an explicit path separator and extension probing mode.</summary>
    /// <param name="environment">The environment variable provider.</param>
    /// <param name="fileSystem">The file system abstraction used to probe candidates.</param>
    /// <param name="pathSeparator">The separator between <c>PATH</c> entries.</param>
    /// <param name="probePathExtensions"><see langword="true"/> to probe <c>PATHEXT</c> extensions (Windows semantics).</param>
    internal ToolValidator(IEnvironmentProvider environment, IFileSystem fileSystem, char pathSeparator, bool probePathExtensions)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _resolver = new ExecutableResolver(environment, fileSystem, pathSeparator, probePathExtensions);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="requiredTools"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An entry of <paramref name="requiredTools"/> is null, empty or whitespace.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is already cancelled.</exception>
    public Task<ToolValidationResult> ValidateRequiredToolsAsync(
        IReadOnlyList<string> requiredTools,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requiredTools);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var tool in requiredTools)
        {
            if (string.IsNullOrWhiteSpace(tool))
            {
                throw new ArgumentException("Required tool names must not be null, empty or whitespace.", nameof(requiredTools));
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        var missing = new List<string>();

        foreach (var tool in requiredTools)
        {
            if (!seen.Add(tool))
            {
                continue;
            }

            var target = _resolver.Resolve(tool) is null ? missing : found;
            target.Add(tool);
        }

        return Task.FromResult(new ToolValidationResult(found.AsReadOnly(), missing.AsReadOnly()));
    }
}
