using System.Runtime.InteropServices;

namespace Stgadgets.Host.Windows;

/// <summary>
/// Just enough DirectComposition for WebView2's visual hosting: a device, a target for the overlay window and a root
/// visual for WebView2 to draw into. A window's own surface can't be transparent per pixel over another app, but
/// DirectComposition content on a window with no redirection bitmap can (docs/decisions/0007-native-overlay-host.md).
/// </summary>
internal sealed class Composition : IDisposable
{
    private readonly IDCompositionDevice _device;
    private readonly IDCompositionTarget _target;
    private readonly IDCompositionVisual _root;

    public Composition(nint window)
    {
        var iid = typeof(IDCompositionDevice).GUID;
        Marshal.ThrowExceptionForHR(DCompositionCreateDevice2(0, ref iid, out var device));
        _device = (IDCompositionDevice)device;
        _target = _device.CreateTargetForHwnd(window, topmost: true);
        _root = _device.CreateVisual();
        _target.SetRoot(_root);
        _device.Commit();
    }

    /// <summary>For <c>CoreWebView2CompositionController.RootVisualTarget</c>.</summary>
    public object RootVisual => _root;

    public void Commit() => _device.Commit();

    public void Dispose()
    {
        _ = Marshal.ReleaseComObject(_root);
        _ = Marshal.ReleaseComObject(_target);
        _ = Marshal.ReleaseComObject(_device);
    }

    // DCompositionCreateDevice2 with no rendering device, as in Microsoft's WebView2 samples: WebView2 brings its own.
    [DllImport("dcomp.dll")]
    private static extern int DCompositionCreateDevice2(nint renderingDevice, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object device);

    // The interfaces from dcomp.h, declared only as far as the methods used here, in their vtable order.
    [ComImport, Guid("C37EA93A-E7AA-450D-B16F-9746CB0407F3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDCompositionDevice
    {
        void Commit();

        void WaitForCommitCompletion();

        void GetFrameStatistics(nint statistics);

        IDCompositionTarget CreateTargetForHwnd(nint window, [MarshalAs(UnmanagedType.Bool)] bool topmost);

        IDCompositionVisual CreateVisual();
    }

    [ComImport, Guid("EACDD04C-117E-4E17-88F4-D1B12B0E3D89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDCompositionTarget
    {
        void SetRoot(IDCompositionVisual visual);
    }

    [ComImport, Guid("4D93059D-097B-4651-9A60-F0F25116E2F3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDCompositionVisual;
}
