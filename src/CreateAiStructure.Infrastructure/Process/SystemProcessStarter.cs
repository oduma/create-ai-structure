using System.Diagnostics;

namespace CreateAiStructure.Infrastructure.Process;

/// <summary>
/// Starts real operating-system processes via <see cref="System.Diagnostics.Process"/>.
/// </summary>
internal sealed class SystemProcessStarter : IProcessStarter
{
    /// <inheritdoc />
    /// <remarks>
    /// With <see cref="ProcessStartInfo.UseShellExecute"/> set to <see langword="false"/> a process is always started
    /// or an exception is thrown, so this implementation never returns <see langword="null"/>.
    /// </remarks>
    public IProcessHandle? Start(ProcessStartInfo startInfo)
    {
        var process = new System.Diagnostics.Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new SystemProcessHandle(process);
    }
}
