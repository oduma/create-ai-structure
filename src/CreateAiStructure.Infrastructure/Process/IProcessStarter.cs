using System.Diagnostics;

namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// Starts operating-system processes; the seam that isolates <see cref="ProcessRunner"/> from <see cref="System.Diagnostics.Process"/>.
/// </summary>
internal interface IProcessStarter
{
    /// <summary>Starts a process described by <paramref name="startInfo"/>.</summary>
    /// <param name="startInfo">The fully prepared start information.</param>
    /// <returns>A handle to the started process, or <see langword="null"/> when no process was started.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">The executable could not be started.</exception>
    /// <exception cref="FileNotFoundException">The executable could not be found.</exception>
    IProcessHandle? Start(ProcessStartInfo startInfo);
}
