using CreateAiStructure.Cli;
using CreateAiStructure.Cli.ConsoleIo;

using var cts = new CancellationTokenSource();
ConsoleCancelEventHandler onCancelKeyPress = (_, e) => { e.Cancel = true; cts.Cancel(); };
Console.CancelKeyPress += onCancelKeyPress;

try
{
    return await CliHost.RunAsync(args, new ConsoleStreams(Console.Out, Console.Error), cts.Token);
}
finally
{
    // Unsubscribe before the token source is disposed so a late Ctrl+C cannot hit a disposed source.
    Console.CancelKeyPress -= onCancelKeyPress;
}
