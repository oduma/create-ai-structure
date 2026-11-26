namespace CreateAiStructure.Domain.Tooling;

/// <summary>The external command-line tools required by the scaffolding workflow.</summary>
public static class RequiredTools
{
    /// <summary>Gets the required tools, in check order.</summary>
    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { "git", "gh", "depviz", "t3" });
}
