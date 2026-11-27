using CreateAiStructure.Cli;
using CreateAiStructure.Cli.ConsoleIo;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

return await CliHost.RunAsync(args, new ConsoleStreams(Console.Out, Console.Error), cts.Token);
