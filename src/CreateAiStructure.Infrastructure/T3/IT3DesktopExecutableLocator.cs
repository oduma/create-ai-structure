using CreateAiStructure.Domain.Common;

namespace CreateAiStructure.Infrastructure.T3;

/// <summary>
/// Discovers the full path of the Windows T3 Code desktop executable.
/// </summary>
public interface IT3DesktopExecutableLocator
{
    /// <summary>
    /// Locates the T3 Code desktop executable.
    /// </summary>
    /// <returns>
    /// A successful result holding the absolute path of the executable, or a failure with the
    /// <c>T3Launch.DesktopExecutableNotFound</c> error when no executable could be found.
    /// </returns>
    Result<string> Locate();
}
