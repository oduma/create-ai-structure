namespace CreateAiStructure.Application.Orchestration;

/// <summary>
/// The outcome of a complete scaffolding run.
/// </summary>
/// <param name="RepositoryDirectory">The local repository directory.</param>
/// <param name="MaterializedFiles">The paths of the files written into the repository.</param>
/// <param name="StartedT3DesktopApp">Whether the T3 Code desktop app was started.</param>
/// <param name="T3ImportReminder">The reminder shown to the user about importing the repository in T3 Code.</param>
public sealed record ScaffoldResult(
    string RepositoryDirectory,
    IReadOnlyList<string> MaterializedFiles,
    bool StartedT3DesktopApp,
    string T3ImportReminder);
