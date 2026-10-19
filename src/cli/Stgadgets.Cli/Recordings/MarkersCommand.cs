using System.CommandLine;

namespace Stgadgets.Cli.Recordings;

/// <summary><c>stgadgets recording markers</c>: the recording's length and markers.</summary>
internal static class MarkersCommand
{
    public static Command Create()
    {
        var recording = RecordingCommand.Argument();
        var command = new Command("markers", "List the recording's markers and the ticks they're at.") { recording };
        command.SetAction(StgadgetsCommand.Handle(result =>
        {
            var loaded = RecordingCommand.Load(result.GetRequiredValue(recording));
            var output = result.InvocationConfiguration.Output;
            output.WriteLine($"{loaded.Count} ticks");
            foreach (var marker in loaded.Manifest.Markers ?? [])
                output.WriteLine($"{marker.Tick,8}  {marker.Label}");
        }));
        return command;
    }
}
