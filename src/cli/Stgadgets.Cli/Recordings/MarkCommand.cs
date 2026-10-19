using System.CommandLine;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Recordings;

/// <summary><c>stgadgets recording mark</c>: add a marker, or move one.</summary>
internal static class MarkCommand
{
    public static Command Create()
    {
        var recording = RecordingCommand.Argument();
        var tick = new Argument<int>("tick") { Description = "The tick to mark." };
        var label = new Argument<string>("label") { Description = "The marker's label; a marker with this label already is moved." };
        var command = new Command("mark", "Add a marker at a tick, or move the marker with this label there.") { recording, tick, label };
        command.SetAction(StgadgetsCommand.Handle(result =>
        {
            var directory = result.GetRequiredValue(recording);
            var loaded = RecordingCommand.Load(directory);
            var (at, name) = (result.GetValue(tick), result.GetRequiredValue(label));
            if (at < 0 || at >= loaded.Count)
                throw new UsageException($"Tick {at} isn't in the recording (0 to {loaded.Count - 1})");
            RecordingEdits.Mark(directory.FullName, at, name);
            result.InvocationConfiguration.Output.WriteLine($"marked tick {at}: {name}");
        }));
        return command;
    }
}
