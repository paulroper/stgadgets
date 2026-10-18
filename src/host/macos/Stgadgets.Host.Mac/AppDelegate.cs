using AppKit;
using CoreGraphics;
using Foundation;
using Stgadgets.Host.Lib.Api;

namespace Stgadgets.Host.Mac;

/// <summary>
/// Menu bar app that keeps an overlay window on top of the emulator's window. The stgadgets server
/// (src/server), which must be running, says which emulator to follow and serves the layout.
/// </summary>
[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    private const double TrackingInterval = 1.0 / 30;   // window tracking only; game data doesn't go through here

    private EmulatorLocator? _locator;
    private OverlayWindow? _overlay;
    private NSStatusItem? _statusItem;
    private NSMenuItem? _stateItem;
    private NSTimer? _timer;
    private CGRect? _lastFrame;

    // Never a real frame: makes Track's next run apply its decision even if the frame hasn't changed.
    private static readonly CGRect Hidden = new(-1, -1, 0, 0);
    private NSObject? _activity;
    private SnapshotServer? _snapshots;

    public override void DidFinishLaunching(NSNotification notification)
    {
        // We're never the active app, so stop macOS from napping or throttling us (docs/requirements.md, L2).
        _activity = NSProcessInfo.ProcessInfo.BeginActivity(
            NSActivityOptions.UserInitiated | NSActivityOptions.LatencyCritical, "Overlay must keep up with the game");

        var server = new Uri(Environment.GetEnvironmentVariable("STGADGETS_SERVER") ?? "http://127.0.0.1:5199/");
        var url = new Uri(server, "?overlay").ToString();
        _locator = new EmulatorLocator(server);
        _overlay = new OverlayWindow(new NSUrl(url));
        CreateStatusItem();

        if (int.TryParse(Environment.GetEnvironmentVariable("STGADGETS_SNAPSHOT_PORT"), out var snapshotPort))
        {
            _snapshots = new SnapshotServer(snapshotPort, _overlay,
                () => _locator.Target is { } target ? EmulatorWindowTracker.FindContentRect(target) : null,
                () => _lastFrame = Hidden);   // forget what Track last did, so its next run shows or hides afresh
            Console.WriteLine($"stgadgets overlay: snapshots at http://127.0.0.1:{snapshotPort}/snapshot");
        }

        _timer = NSTimer.CreateRepeatingScheduledTimer(TrackingInterval, _ => Track());

        if (Experiment("activate"))
        {
            NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Regular;
            NSApplication.SharedApplication.Activate();
        }
        if (Experiments.Length > 0)
            Console.WriteLine($"stgadgets overlay: experiments {string.Join(",", Experiments)}");
        Console.WriteLine($"stgadgets overlay: loading {url}");
    }

    public override void WillTerminate(NSNotification notification)
    {
        _timer?.Invalidate();
        _locator?.Dispose();
        _snapshots?.Dispose();
        if (_activity is not null)
            NSProcessInfo.ProcessInfo.EndActivity(_activity);
    }

    // Spike experiments (STGADGETS_EXPERIMENT=always-show,activate,keyable,normal-level): see docs/latency.md.
    private static readonly string[] Experiments =
        (Environment.GetEnvironmentVariable("STGADGETS_EXPERIMENT") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);

    internal static bool Experiment(string name) => Experiments.Contains(name);

    private void Track()
    {
        var target = _locator!.Target;
        CGRect? frame = (target, Experiment("always-show")) switch
        {
            (null, true) => new CGRect(100, 100, 400, 600),
            (null, false) => null,
            ({ } t, true) => EmulatorWindowTracker.FindContentRect(t) ?? new CGRect(100, 100, 400, 600),
            ({ } t, false) => EmulatorWindowTracker.IsFrontmost(t) ? EmulatorWindowTracker.FindContentRect(t) : null,
        };

        var state = (target, frame) switch
        {
            (null, _) => _locator.ServerReachable ? "Waiting for an emulator" : "stgadgets server not running",
            (_, not null) => $"Following {target.AppName}",
            _ => EmulatorWindowTracker.IsFrontmost(target) ? $"No {target.AppName} window" : $"Waiting for {target.AppName}",
        };
        if (state != _stateItem!.Title)
            SetState(state);

        if (frame == _lastFrame)
            return;
        _lastFrame = frame;

        if (frame is { } f)
        {
            _overlay!.SetFrame(f, display: true);
            _overlay.OrderFrontRegardless();   // show without activating our app
        }
        else
        {
            _overlay!.OrderOut(null);
        }
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
