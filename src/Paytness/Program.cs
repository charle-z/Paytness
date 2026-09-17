using System.CommandLine;
using System.CommandLine.Parsing;
using Paytness.Cli;

RootCommand root = CliApp.Build();
ParseResult parseResult = root.Parse(args);
if (parseResult.Errors.Count > 0)
{
    foreach (ParseError error in parseResult.Errors)
    {
        Console.Error.WriteLine(error.Message);
    }

    return 2;
}

using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
Console.CancelKeyPress += cancelHandler;
try
{
    return await CliProcess.InvokeAsync(parseResult, cancellation.Token);
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
