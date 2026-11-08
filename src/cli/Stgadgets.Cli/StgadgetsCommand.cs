using System.CommandLine;
using Stgadgets.Cli.Dev;
using Stgadgets.Cli.Latency;
using Stgadgets.Cli.Peek;
using Stgadgets.Cli.Recordings;

namespace Stgadgets.Cli;

/// <summary>The <c>stgadgets</c> command and its subcommands. See docs/cli.md.</summary>
internal static class StgadgetsCommand
{
    public static RootCommand Create() => new("Tools for developing stgadgets game packs.")
    {
        RecordingCommand.Create(),
        DevCommand.Create(),
        LatencyCommand.Create(),
        PeekCommand.Create(),
    };

    /// <summary>
    /// Runs <paramref name="action"/>, reporting a problem with the user's input or files as a message and exit code 1
    /// rather than a stack trace.
    /// </summary>
    public static Func<ParseResult, int> Handle(Action<ParseResult> action) => result =>
    {
        try
        {
            action(result);
            return 0;
        }
        catch (Exception ex) when (ex is UsageException or FormatException or IOException)
        {
            result.InvocationConfiguration.Error.WriteLine(ex.Message);
            return 1;
        }
    };

    /// <inheritdoc cref="Handle"/>
    public static Func<ParseResult, CancellationToken, Task<int>> HandleAsync(Func<ParseResult, CancellationToken, Task> action) =>
        async (result, cancellationToken) =>
        {
            try
            {
                await action(result, cancellationToken);
                return 0;
            }
            catch (Exception ex) when (ex is UsageException or FormatException or IOException or TimeoutException)
            {
                await result.InvocationConfiguration.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        };
}

/// <summary>Something wrong with what the user asked for, such as a marker that doesn't exist.</summary>
internal sealed class UsageException(string message) : Exception(message);
