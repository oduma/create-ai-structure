namespace CreateAiStructure.Cli.Tests.Publishing;

/// <summary>A <see cref="FactAttribute"/> that is skipped on every operating system other than Windows.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "The standalone publish verifies the win-x64 single-file executable and only runs on Windows.";
        }
    }
}
