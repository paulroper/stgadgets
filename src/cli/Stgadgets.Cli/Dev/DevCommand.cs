using System.CommandLine;
using Stgadgets.Providers;

namespace Stgadgets.Cli.Dev;

/// <summary>
/// <c>stgadgets dev</c>: start, check and stop the server and the overlay while developing, and take snapshots, so a
/// change can be seen in one step (docs/cli.md#development).
/// </summary>
internal static class DevCommand
{
    public static Command Create() => new("dev", "Run stgadgets for development: start, restart, status, snapshot and stop.")
    {
        Start(),
        Restart(),
        Status(),
        Snapshot(),
        Stop(),
    };

    // Options shared by start and restart. On restart, those not given keep their values from the last start.
    private sealed class StackOptions
    {
        public Option<string> Provider { get; } = new("--provider")
        {
            Description = $"The emulator to read from: {string.Join(", ", EmulatorProviders.All.Keys)}. Default: retroarch, or the last start's.",
        };

        public Option<string> Endpoint { get; } = new("--endpoint")
        {
            Description = "Where the emulator listens, in the provider's form; for replay, a recording's folder (relative to the repository), " +
                "optionally #<marker> to hold there.",
        };

        public Option<bool?> Overlay { get; } = new("--overlay") { Description = "Also run the macOS overlay, with snapshots on." };

        public Option<bool> BuildUi { get; } = new("--build-ui") { Description = "Build the UI first (npm run build in src/ui)." };

        public Option<int?> SnapshotPort { get; } = new("--snapshot-port") { Description = "The overlay's snapshot port (5198)." };

        public void AddTo(Command command)
        {
            Provider.AcceptOnlyFromAmong([.. EmulatorProviders.All.Keys]);
            foreach (var option in (Option[])[Provider, Endpoint, Overlay, BuildUi, SnapshotPort])
                command.Options.Add(option);
        }

        public DevOptions Read(ParseResult result, DevOptions? last) =>
            DevOptions.From(last, result.GetValue(Provider), result.GetValue(Endpoint), result.GetValue(Overlay), result.GetValue(SnapshotPort));
    }

    private static DevStack Stack(ParseResult result) =>
        new(DevStack.FindRepoRoot(Directory.GetCurrentDirectory()), result.InvocationConfiguration.Output);

    private static Command Start()
    {
        var options = new StackOptions();
        var command = new Command(
            "start",
            "Start the server (and the overlay with --overlay) in the background, and wait until they're ready. " +
            "Logs go to .stgadgets/dev/.");
        options.AddTo(command);
        command.SetAction(StgadgetsCommand.HandleAsync((result, cancellationToken) =>
            Stack(result).StartAsync(options.Read(result, last: null), result.GetValue(options.BuildUi), cancellationToken)));
        return command;
    }

    private static Command Restart()
    {
        var options = new StackOptions();
        var command = new Command(
            "restart",
            "Stop and start again, after changing map.yaml or derived.js, say. Options not given keep their values from the last start.");
        options.AddTo(command);
        command.SetAction(StgadgetsCommand.HandleAsync(async (result, cancellationToken) =>
        {
            var stack = Stack(result);
            var next = options.Read(result, stack.Load()?.Options);
            await stack.StopAsync();
            await stack.StartAsync(next, result.GetValue(options.BuildUi), cancellationToken);
        }));
        return command;
    }

    private static Command Status()
    {
        var command = new Command("status", "What's running, and what the server is doing.");
        command.SetAction(StgadgetsCommand.HandleAsync((result, cancellationToken) => Stack(result).WriteStatusAsync(cancellationToken)));
        return command;
    }

    private static Command Snapshot()
    {
        var file = new Option<FileInfo>("--out") { Description = "Where to save the PNG (.stgadgets/dev/snapshot.png)." };
        var width = new Option<int?>("--width") { Description = "Scale the picture to this many pixels wide." };
        var command = new Command("snapshot", "Save a PNG of the layout as the overlay draws it (needs --overlay).") { file, width };
        command.SetAction(StgadgetsCommand.HandleAsync(async (result, cancellationToken) =>
        {
            var root = DevStack.FindRepoRoot(Directory.GetCurrentDirectory());
            var path = result.GetValue(file)?.FullName ?? Path.Combine(root, ".stgadgets", "dev", "snapshot.png");
            var visible = await new DevStack(root, result.InvocationConfiguration.Output).SaveSnapshotAsync(path, result.GetValue(width), cancellationToken);
            result.InvocationConfiguration.Output.WriteLine($"{path}{(visible ? "" : " (the overlay is hidden; drawn off screen)")}");
        }));
        return command;
    }

    private static Command Stop()
    {
        var command = new Command("stop", "Stop what `stgadgets dev start` started.");
        command.SetAction(StgadgetsCommand.HandleAsync((result, _) => Stack(result).StopAsync()));
        return command;
    }
}
