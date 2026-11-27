namespace CreateAiStructure.Cli.Parsing;

/// <summary>The static usage text for <c>create-ai-structure</c>.</summary>
public static class CliUsage
{
    /// <summary>Gets the usage text: synopsis, option reference, supported project types and an example.</summary>
    public const string Text = """
        Usage: create-ai-structure --type <webApp|api|console|MAUI> --repo <repository-name>

        Options:
          --type <type>     The project type to scaffold (case-insensitive). Also accepts --type=<type>.
          --repo <name>     The name of the repository to create. Also accepts --repo=<name>.
          -h|--help         Show this help text and exit.
          -v|--version      Show the tool version and exit.

        Supported project types:
          webApp, api, console, MAUI

        Example:
          create-ai-structure --type webApp --repo my-app
        """;
}
