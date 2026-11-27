using System.Reflection;

namespace CreateAiStructure.Cli;

/// <summary>Resolves the display version of an assembly.</summary>
public static class CliVersion
{
    /// <summary>
    /// Returns the <see cref="AssemblyInformationalVersionAttribute"/> of <paramref name="assembly"/>,
    /// falling back to its <see cref="AssemblyName.Version"/> when the attribute is absent.
    /// </summary>
    /// <param name="assembly">The assembly whose version is resolved.</param>
    /// <returns>The version text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    public static string Resolve(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return informational is null
            ? $"{assembly.GetName().Version}"
            : informational.InformationalVersion;
    }
}
