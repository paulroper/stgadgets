using Stgadgets.Host.Lib.Api;

namespace Stgadgets.Host.Lib.Overlay;

/// <summary>A rectangle in the host OS's own screen coordinates, whatever their origin and units.</summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height);

/// <summary>The OS's view of the emulator's windows, implemented by each host.</summary>
public interface IEmulatorWindows
{
    /// <summary>The emulator's content area (its window without the title bar), or null if it has no visible window.</summary>
    ScreenRect? FindContentRect(EmulatorTarget target);

    /// <summary>True when the emulator is the active app, i.e. the user is looking at it.</summary>
    bool IsFrontmost(EmulatorTarget target);
}

/// <summary>The overlay window, implemented by each host. Showing it must never take focus from the emulator.</summary>
public interface IOverlayWindow
{
    void ShowAt(ScreenRect frame);

    void Hide();
}

/// <summary>
/// Decides, each time the host's timer fires, where the overlay should be and whether it should show, and tells the
/// window only when that changes. The overlay shows over the emulator's content area while the emulator is the active
/// app, and hides otherwise.
/// </summary>
/// <param name="stateChanged">Called with a line for the user, such as "Following RetroArch", whenever it changes.</param>
public sealed class OverlayTracker(IEmulatorWindows windows, IOverlayWindow overlay, bool alwaysShow, Action<string> stateChanged)
{
    /// <summary>Where an <c>always-show</c> overlay goes when there's no emulator window to follow.</summary>
    public static readonly ScreenRect Placeholder = new(100, 100, 400, 600);

    private ScreenRect? _frame;
    private bool _applied;
    private string? _state;

    /// <param name="target">The emulator to follow (<see cref="EmulatorLocator.Target"/>).</param>
    /// <param name="serverReachable"><see cref="EmulatorLocator.ServerReachable"/>.</param>
    public void Track(EmulatorTarget? target, bool serverReachable)
    {
        var frame = FrameFor(target);
        var state = StateFor(target, frame, serverReachable);
        if (state != _state)
        {
            _state = state;
            stateChanged(state);
        }

        if (_applied && frame == _frame)
            return;
        _frame = frame;
        _applied = true;

        if (frame is { } f)
            overlay.ShowAt(f);
        else
            overlay.Hide();
    }

    /// <summary>
    /// Forgets what the window was last told, so the next <see cref="Track"/> shows or hides it afresh. For when
    /// something else, such as a snapshot, has moved or hidden the window.
    /// </summary>
    public void Forget() => _applied = false;

    private ScreenRect? FrameFor(EmulatorTarget? target) => (target, alwaysShow) switch
    {
        (null, true) => Placeholder,
        (null, false) => null,
        ({ } t, true) => windows.FindContentRect(t) ?? Placeholder,
        ({ } t, false) => windows.IsFrontmost(t) ? windows.FindContentRect(t) : null,
    };

    private string StateFor(EmulatorTarget? target, ScreenRect? frame, bool serverReachable) => (target, frame) switch
    {
        (null, _) => serverReachable ? "Waiting for an emulator" : "stgadgets server not running",
        (_, not null) => $"Following {target.AppName}",
        _ => windows.IsFrontmost(target) ? $"No {target.AppName} window" : $"Waiting for {target.AppName}",
    };
}
