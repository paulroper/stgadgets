using System.Diagnostics;
using System.Runtime.InteropServices;
using Stgadgets.Host.Lib.Api;
using Stgadgets.Host.Lib.Overlay;

namespace Stgadgets.Host.Windows;

/// <summary>
/// Finds the emulator's window with EnumWindows. Positions are in physical pixels: the host is per-monitor DPI aware
/// (the project's ApplicationHighDpiMode), so Windows doesn't scale what it reports.
/// </summary>
internal sealed class EmulatorWindowTracker : IEmulatorWindows
{
    private const uint GwOwner = 4;          // GW_OWNER
    private const int DwmwaCloaked = 14;     // DWMWA_CLOAKED

    public bool IsFrontmost(EmulatorTarget target) => GetForegroundWindow() is var window && window != 0 && IsOwnedBy(window, target);

    /// <summary>
    /// The client area (the window without its title bar and borders) of the largest visible top-level window the
    /// emulator owns, matched by process ID when known.
    /// </summary>
    public ScreenRect? FindContentRect(EmulatorTarget target)
    {
        nint best = 0;
        long bestArea = 0;
        _ = EnumWindows((window, _) =>
        {
            if (IsCandidate(window) && IsOwnedBy(window, target) && GetClientRect(window, out var client) != 0)
            {
                var area = (long)client.Right * client.Bottom;
                if (area > bestArea)
                {
                    best = window;
                    bestArea = area;
                }
            }
            return 1;   // keep going
        }, 0);

        if (best == 0 || GetClientRect(best, out var content) == 0)
            return null;
        var origin = new Point();
        _ = ClientToScreen(best, ref origin);
        return new ScreenRect(origin.X, origin.Y, content.Right, content.Bottom);
    }

    /// <summary>A visible, unminimised top-level window, not owned by another (dialogs are) or cloaked (on another virtual desktop).</summary>
    private static bool IsCandidate(nint window) =>
        IsWindowVisible(window) != 0
        && IsIconic(window) == 0
        && GetWindow(window, GwOwner) == 0
        && !(DwmGetWindowAttribute(window, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0);

    private static bool IsOwnedBy(nint window, EmulatorTarget target)
    {
        _ = GetWindowThreadProcessId(window, out var processId);
        if (target.ProcessId is { } pid)
            return processId == pid;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, target.AppName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;   // exited
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    private delegate int EnumWindowsProc(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern int EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern int IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern int IsIconic(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern int GetClientRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern int ClientToScreen(nint window, ref Point point);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
}
