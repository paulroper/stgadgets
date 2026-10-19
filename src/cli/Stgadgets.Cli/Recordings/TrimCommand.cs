using System.CommandLine;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Recordings;

/// <summary><c>stgadgets recording trim</c>: copy a slice of a recording to a new, smaller one.</summary>
internal static class TrimCommand
{
    public static Command Create()
    {
        var recording = RecordingCommand.Argument();
        var first = new Argument<string>("first") { Description = $"The first tick to keep: {RecordingCommand.PointHelp}." };
        var last = new Argument<string>("last") { Description = "The last tick to keep, the same way." };
        var output = new Option<DirectoryInfo>("--out") { Description = "The new recording's folder; it mustn't exist yet.", Required = true };
        var command = new Command("trim", "Copy a slice of a recording, with its markers, to a new recording; timestamps start again from 0.")
        {
            recording, first, last, output,
        };
        command.SetAction(StgadgetsCommand.Handle(result =>
        {
            var directory = result.GetRequiredValue(recording);
            var loaded = RecordingCommand.Load(directory);
            var (from, to) = (RecordingCommand.Tick(loaded, result.GetRequiredValue(first)), RecordingCommand.Tick(loaded, result.GetRequiredValue(last)));
            if (from > to)
                throw new UsageException($"The first tick ({from}) is after the last ({to})");
            var target = result.GetRequiredValue(output).FullName;
            RecordingEdits.Trim(directory.FullName, from, to, target);
            result.InvocationConfiguration.Output.WriteLine($"wrote {to - from + 1} ticks to {target}");
        }));
        return command;
    }
}
