namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// Resolves a bare tool name to an absolute executable path by searching the <c>PATH</c> environment variable.
/// </summary>
internal interface IExecutableResolver
{
    /// <summary>Resolves a bare tool name to the absolute path of the first matching executable.</summary>
    /// <param name="toolName">The bare tool name, for example <c>git</c> or <c>git.exe</c>.</param>
    /// <returns>The absolute path of the first match, or <see langword="null"/> when nothing matches.</returns>
    string? Resolve(string toolName);
}
