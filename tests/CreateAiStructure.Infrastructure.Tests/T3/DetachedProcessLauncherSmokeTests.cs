using CreateAiStructure.Application.Runtime;
using CreateAiStructure.Infrastructure.T3;
using FluentAssertions;

namespace CreateAiStructure.Infrastructure.Tests.T3;

public class DetachedProcessLauncherSmokeTests
{
    private const string DotnetHostPathVariable = "DOTNET_HOST_PATH";

    [Fact]
    public void StartDetached_WithDefaultStarter_LaunchesDotnetHostSuccessfully()
    {
        var hostPath = ResolveDotnetHostPath();
        Path.GetFileNameWithoutExtension(hostPath).Should().BeEquivalentTo(
            "dotnet",
            "the smoke test must launch the non-GUI dotnet host, never testhost");
        var launcher = new DetachedProcessLauncher();

        var result = launcher.StartDetached(new ProcessExecutionRequest(hostPath, ["--version"]));

        result.IsSuccess.Should().BeTrue();
    }

    private static string ResolveDotnetHostPath()
    {
        var hostPath = Environment.GetEnvironmentVariable(DotnetHostPathVariable);
        return !string.IsNullOrWhiteSpace(hostPath) && Path.IsPathFullyQualified(hostPath) && File.Exists(hostPath)
            ? hostPath
            : Environment.ProcessPath ?? string.Empty;
    }
}
