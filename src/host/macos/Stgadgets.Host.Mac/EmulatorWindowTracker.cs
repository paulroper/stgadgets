using System.Runtime.InteropServices;
using ObjCRuntime;
using Stgadgets.Host.Lib.Api;
using Stgadgets.Host.Lib.Overlay;

namespace Stgadgets.Host.Mac;

/// <summary>
/// Finds the emulator's window on screen using CGWindowListCopyWindowInfo. Owner PIDs, names and bounds don't need the
/// Screen Recording permission (window titles would).
/// </summary>
public sealed class EmulatorWindowTracker : IEmulatorWindows
{
    private const uint OnScreenOnly = 1 << 0;           // kCGWindowListOptionOnScreenOnly
    private const uint ExcludeDesktopElements = 1 << 4; // kCGWindowListExcludeDesktopElements

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    public bool IsFrontmost(EmulatorTarget target) =>
        NSWorkspace.SharedWorkspace.FrontmostApplication is { } app
        && (target.ProcessId is { } pid ? app.ProcessIdentifier == pid : app.LocalizedName == target.AppName);

    /// <summary>
    /// The emulator's content area in Cocoa screen coordinates. Uses the largest normal-level window the app owns,
    /// matched by process ID when known.
    /// </summary>
    public ScreenRect? FindContentRect(EmulatorTarget target)
    {
        using var windows = Runtime.GetNSObject<NSArray>(
            CGWindowListCopyWindowInfo(OnScreenOnly | ExcludeDesktopElements, 0), owns: true);
        if (windows is null)
            return null;

        CGRect? best = null;
        for (nuint i = 0; i < windows.Count; i++)
        {
            var info = windows.GetItem<NSDictionary>(i);
            if (info is null
                || !IsOwnedBy(info, target)
                || (info["kCGWindowLayer"] as NSNumber)?.Int32Value != 0
                || info["kCGWindowBounds"] is not NSDictionary bounds)
            {
                continue;
            }

            var rect = new CGRect(Number(bounds, "X"), Number(bounds, "Y"), Number(bounds, "Width"), Number(bounds, "Height"));
            if (best is null || rect.Width * rect.Height > best.Value.Width * best.Value.Height)
                best = rect;
        }

        if (best is not { } quartz)
            return null;
        var content = ToCocoa(WithoutTitleBar(quartz));
        return new ScreenRect(content.X, content.Y, content.Width, content.Height);
    }

    private static bool IsOwnedBy(NSDictionary info, EmulatorTarget target) => target.ProcessId is { } pid
        ? (info["kCGWindowOwnerPID"] as NSNumber)?.Int32Value == pid
        : (info["kCGWindowOwnerName"] as NSString)?.ToString() == target.AppName;

    private static double Number(NSDictionary dictionary, string key) => (dictionary[key] as NSNumber)?.DoubleValue ?? 0;

    /// <summary>Fullscreen windows have no title bar; windowed ones get the standard height removed from the top.</summary>
    private static CGRect WithoutTitleBar(CGRect quartzRect)
    {
        if (NSScreen.Screens.Any(s => ToQuartz(s.Frame) == quartzRect))
            return quartzRect;

        var titleBar = NSWindow.FrameRectFor(new CGRect(0, 0, 100, 100), NSWindowStyle.Titled).Height - 100;
        return new CGRect(quartzRect.X, quartzRect.Y + titleBar, quartzRect.Width, quartzRect.Height - titleBar);
    }

    // Quartz window coordinates have their origin at the top-left of the primary screen, y down.
    // Cocoa screen coordinates have their origin at the bottom-left of the primary screen, y up.
    private static nfloat PrimaryScreenHeight => NSScreen.Screens[0].Frame.Height;

    private static CGRect ToCocoa(CGRect quartz) =>
        new(quartz.X, PrimaryScreenHeight - quartz.Y - quartz.Height, quartz.Width, quartz.Height);

    private static CGRect ToQuartz(CGRect cocoa) =>
        new(cocoa.X, PrimaryScreenHeight - cocoa.Y - cocoa.Height, cocoa.Width, cocoa.Height);
}
