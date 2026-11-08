using Stgadgets.Host.Lib.Api;
using Stgadgets.Host.Lib.Overlay;

namespace Stgadgets.Host.Mac;

/// <summary>
/// Menu bar app that keeps an overlay window on top of the emulator's window. The stgadgets server
/// (src/server), which must be running, says which emulator to follow and serves the layout.
/// </summary>
[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    private const double TrackingInterval = 1.0 / 30;   // window tracking only; game data doesn't go through here

    // Mac spike experiments, on top of always-show (OverlaySettings): activate, keyable, normal-level. See docs/latency.md.
    internal static readonly OverlaySettings Settings = OverlaySettings.FromEnvironment();

    private readonly EmulatorWindowTracker _windows = new();
    private EmulatorLocator? _locator;
    private OverlayWindow? _overlay;
    private OverlayTracker? _tracker;
    private NSStatusItem? _statusItem;
    private NSMenuItem? _stateItem;
    private NSTimer? _timer;
    private NSObject? _activity;
    private SnapshotServer? _snapshots;

    internal static bool Experiment(string name) => Settings.Experiment(name);

    public override void DidFinishLaunching(NSNotification notification)
    {
        // We're never the active app, so stop macOS from napping or throttling us (docs/requirements.md, L2).
        _activity = NSProcessInfo.ProcessInfo.BeginActivity(
            NSActivityOptions.UserInitiated | NSActivityOptions.LatencyCritical, "Overlay must keep up with the game");

        var locator = _locator = new EmulatorLocator(Settings.Server);
        var overlay = _overlay = new OverlayWindow(new NSUrl(Settings.LayoutUrl.ToString()));
        CreateStatusItem();
        var tracker = _tracker = new OverlayTracker(_windows, overlay, Settings.AlwaysShow, SetState);

        if (Settings.SnapshotPort is { } snapshotPort)
        {
            _snapshots = new SnapshotServer(snapshotPort, width => overlay.SnapshotAsync(
                () => locator.Target is { } target ? _windows.FindContentRect(target) : null,
                width,
                tracker.Forget));   // the snapshot showed or hid the window behind the tracker's back
            Console.WriteLine($"stgadgets overlay: snapshots at http://127.0.0.1:{snapshotPort}/snapshot");
        }

        _timer = NSTimer.CreateRepeatingScheduledTimer(TrackingInterval, _ => tracker.Track(locator.Target, locator.ServerReachable));

        if (Experiment("activate"))
        {
            NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Regular;
            NSApplication.SharedApplication.Activate();
        }
        if (Settings.Experiments.Count > 0)
            Console.WriteLine($"stgadgets overlay: experiments {string.Join(",", Settings.Experiments)}");
        Console.WriteLine($"stgadgets overlay: loading {Settings.LayoutUrl}");
    }

    public override void WillTerminate(NSNotification notification)
    {
        _timer?.Invalidate();
        _locator?.Dispose();
        _snapshots?.Dispose();
        if (_activity is not null)
            NSProcessInfo.ProcessInfo.EndActivity(_activity);
    }

    private void CreateStatusItem()
    {
        _statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
        _statusItem.Button.Title = "STG";

        _stateItem = new NSMenuItem("Starting") { Enabled = false };
        var showBounds = new NSMenuItem("Show overlay bounds");
        showBounds.Activated += (_, _) =>
        {
            _overlay!.ShowBounds = !_overlay.ShowBounds;
            showBounds.State = _overlay.ShowBounds ? NSCellStateValue.On : NSCellStateValue.Off;
        };

        var menu = new NSMenu { AutoEnablesItems = false };
        menu.AddItem(_stateItem);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(new NSMenuItem("Reload layout", (_, _) => _overlay!.Reload()));
        menu.AddItem(showBounds);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(new NSMenuItem("Quit stgadgets", "q", (_, _) => NSApplication.SharedApplication.Terminate(this)));
        _statusItem.Menu = menu;
    }

    private void SetState(string text)
    {
        _stateItem!.Title = text;
        Console.WriteLine($"stgadgets overlay: {text}");
    }
}
