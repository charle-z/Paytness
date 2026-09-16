using System.CommandLine;
using System.CommandLine.Invocation;
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

return await parseResult.InvokeAsync(new InvocationConfiguration(), CancellationToken.None);
