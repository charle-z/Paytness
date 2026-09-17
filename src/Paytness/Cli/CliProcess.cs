using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Paytness.Cli;

public static class CliProcess
{
    public static async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        if (cancellationToken.IsCancellationRequested)
            return Cancelled();

        try
        {
            int exitCode = await parseResult.InvokeAsync(new InvocationConfiguration(), cancellationToken);
            return cancellationToken.IsCancellationRequested && exitCode != 130
                ? Cancelled()
                : exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }
    }

    private static int Cancelled()
    {
        Console.Error.WriteLine("Run cancelled.");
        return 130;
    }
}
