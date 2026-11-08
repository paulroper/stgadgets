using System.CommandLine;
using Stgadgets.Cli.Dev;

namespace Stgadgets.Cli.Recordings;

/// <summary>
/// <c>stgadgets recording play</c>: the dev stack with the server replaying a recording instead of reading an
/// emulator, so gadgets can be seen at a moment of real play with nobody playing. It's the one way to choose what's
/// replayed: on a running stack it restarts it with this recording, keeping the overlay's options. The rest of the
/// stack's commands (<c>dev restart</c>, <c>snapshot</c>, <c>stop</c>) work on it as on live play.
/// </summary>
internal static class PlayCommand
{
    public static Command Create()
    {
        var recording = RecordingCommand.Argument();
        var at = new Option<string>("--at")
        {
            Description = $"Fast-forward to a point and hold there: {RecordingCommand.PointHelp}. Default: play in real time from the start.",
        };
        var overlay = new Option<bool?>("--overlay")
        {
            Description = "Also run the macOS overlay, with snapshots on. Default: as the running stack, else no.",
        };
        var buildUi = new Option<bool>("--build-ui") { Description = "Build the UI first (npm run build in src/ui)." };
        var snapshotPort = new Option<int?>("--snapshot-port") { Description = "The overlay's snapshot port (5198)." };
        var command = new Command(
            "play",
            "Start the server replaying the recording, as `stgadgets dev start` does for live play (and the overlay with --overlay), " +
            "or restart the running dev stack with it. Stop it with `stgadgets dev stop`.")
        {
            recording, at, overlay, buildUi, snapshotPort,
        };
        command.SetAction(StgadgetsCommand.HandleAsync(async (result, cancellationToken) =>
        {
            var directory = result.GetRequiredValue(recording);
            var loaded = RecordingCommand.Load(directory);
            var holdAt = result.GetValue(at) is { } point ? RecordingCommand.Tick(loaded, point) : (int?)null;

            var root = DevStack.FindRepoRoot(Directory.GetCurrentDirectory());
            var endpoint = Path.GetRelativePath(root, directory.FullName) + (holdAt is { } tick ? $"#{tick}" : "");
            var output = result.InvocationConfiguration.Output;
            var stack = new DevStack(root, output);
            var running = stack.Load();
            var options = DevOptions.From(running?.Options, "replay", endpoint, result.GetValue(overlay), result.GetValue(snapshotPort));
            var building = result.GetValue(buildUi);

            // Playing what's already playing changes nothing, so it can be run before every snapshot; a fresh UI build
            // needs a restart, though, for the overlay to load it.
            if (running?.Options == options && !building && await DevStack.StatusAsync(cancellationToken) is not null)
            {
                output.WriteLine($"Already playing {endpoint}");
                return;
            }
            if (running is not null)
            {
                if (running.Options.Provider != "replay")
                    output.WriteLine($"Leaving live play ({running.Options.Provider}) to replay {endpoint}");
                await stack.StopAsync();
            }
            await stack.StartAsync(options, building, cancellationToken);
        }));
        return command;
    }
}
