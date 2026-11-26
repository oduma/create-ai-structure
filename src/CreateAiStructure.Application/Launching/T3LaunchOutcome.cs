namespace CreateAiStructure.Application.Launching;

/// <summary>
/// The outcome of launching T3 Code for a scaffolded repository.
/// </summary>
/// <param name="StartedDesktopApp">Whether the T3 Code desktop app was started.</param>
/// <param name="ReminderMessage">The reminder shown to the user about importing the repository.</param>
public sealed record T3LaunchOutcome(bool StartedDesktopApp, string ReminderMessage);
