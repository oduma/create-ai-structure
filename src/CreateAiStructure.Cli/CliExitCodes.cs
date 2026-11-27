namespace CreateAiStructure.Cli;

/// <summary>The process exit codes returned by <c>create-ai-structure</c>.</summary>
public static class CliExitCodes
{
    /// <summary>The command completed successfully (including help and version output).</summary>
    public const int Success = 0;

    /// <summary>The scaffolding workflow failed.</summary>
    public const int ExecutionFailure = 1;

    /// <summary>The command line could not be parsed.</summary>
    public const int UsageError = 2;

    /// <summary>The operation was cancelled (conventional 128 + SIGINT).</summary>
    public const int Cancelled = 130;
}
