using CreateAiStructure.Application.Runtime;

namespace CreateAiStructure.Infrastructure.Tests.Process.TestSupport;

/// <summary>Test-only thin wrapper over <see cref="Environment"/> for real-process smoke tests.</summary>
internal sealed class RealEnvironmentProvider : IEnvironmentProvider
{
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetLocalApplicationDataPath() => throw new NotSupportedException();

    public string GetCurrentDirectory() => throw new NotSupportedException();
}
