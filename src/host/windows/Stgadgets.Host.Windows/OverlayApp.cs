using Stgadgets.Host.Lib.Api;
using Stgadgets.Host.Lib.Overlay;

namespace Stgadgets.Host.Windows;

/// <summary>
/// Tray app that keeps an overlay window on top of the emulator's window. The stgadgets server (src/server), which
/// must be running, says which emulator to follow and serves the layout.
/// </summary>
internal sealed class OverlayApp : ApplicationContext
{
    private const int TrackingIntervalMs = 33;   // window tracking only; game data doesn't go through here

    private readonly EmulatorLocator _locator;
    private readonly OverlayWindow _overlay;
    private readonly OverlayTracker _tracker;
    private readonly EmulatorWindowTracker _windows = new();
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _stateItem = new("Starting") { Enabled = false };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = TrackingIntervalMs };
    private readonly SnapshotServer? _snapshots;

    public OverlayApp(OverlaySettings settings)
    {
        _locator = new EmulatorLocator(settings.Server);
        _overlay = new OverlayWindow(settings.LayoutUrl);
        _tracker = new OverlayTracker(_windows, _overlay, settings.AlwaysShow, SetState);
        _tray = CreateTray();

        if (settings.SnapshotPort is { } snapshotPort)
        {
            _snapshots = new SnapshotServer(snapshotPort, width => _overlay.SnapshotAsync(
                () => _locator.Target is { } target ? _windows.FindContentRect(target) : null,
                width,
                _tracker.Forget));   // the snapshot moved the window behind the tracker's back
            Console.WriteLine($"stgadgets overlay: snapshots at http://127.0.0.1:{snapshotPort}/snapshot");
        }

        _timer.Tick += (_, _) => _tracker.Track(_locator.Target, _locator.ServerReachable);
        _timer.Start();

        if (settings.Experiments.Count > 0)
            Console.WriteLine($"stgadgets overlay: experiments {string.Join(",", settings.Experiments)}");
        Console.WriteLine($"stgadgets overlay: loading {settings.LayoutUrl}");
    }

    private NotifyIcon CreateTray()
    {
        var showBounds = new ToolStripMenuItem("Show overlay bounds") { CheckOnClick = true };
        showBounds.CheckedChanged += (_, _) => _overlay.ShowBounds(showBounds.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_stateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Reload layout", null, (_, _) => _overlay.Reload());
        menu.Items.Add(showBounds);
        menu.Items.Add("Open DevTools", null, (_, _) => _overlay.OpenDevTools());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit stgadgets", null, (_, _) => ExitThread());

        return new NotifyIcon { Icon = SystemIcons.Application, Text = "stgadgets", ContextMenuStrip = menu, Visible = true };
    }

    private void SetState(string text)
    {
        _stateItem.Text = text;
        Console.WriteLine($"stgadgets overlay: {text}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _snapshots?.Dispose();
            _locator.Dispose();
            _tray.Visible = false;   // otherwise the icon stays in the tray until the mouse passes over it
            _tray.Dispose();
            _overlay.Dispose();
        }
        base.Dispose(disposing);
    }
}
