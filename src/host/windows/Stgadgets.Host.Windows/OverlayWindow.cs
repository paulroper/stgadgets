using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Stgadgets.Host.Lib.Overlay;

namespace Stgadgets.Host.Windows;

/// <summary>
/// A borderless, transparent, click-through window that stays above the emulator, never takes focus, and shows a
/// layout in WebView2. WebView2 draws through DirectComposition (<see cref="Composition"/>), the only way to get a
/// background that's transparent per pixel. See docs/decisions/0007-native-overlay-host.md.
/// </summary>
internal sealed class OverlayWindow : Form, IOverlayWindow
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;   // with WS_EX_LAYERED: clicks go to the window underneath
    private const int WsExTopmost = 0x00000008;
    private const int WsExToolWindow = 0x00000080;    // not in Alt-Tab or the taskbar
    private const int WsExNoActivate = 0x08000000;
    private const int WsExNoRedirectionBitmap = 0x00200000;   // nothing drawn but the DirectComposition content
    private const uint LwaAlpha = 0x2;
    private const uint SwpNoActivate = 0x0010, SwpShowWindow = 0x0040, SwpNoZOrder = 0x0004;
    private const int SwHide = 0;
    private static readonly nint HwndTopmost = -1;

    // WebView2 doesn't draw a window that isn't on screen, so a hidden overlay is drawn at its new size for this long
    // before its snapshot: enough to lay out and paint.
    private static readonly TimeSpan HiddenSnapshotDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    // A layered window shows nothing until it's given its attributes; this experiment leaves the style off to check
    // transparency on its own (the overlay then takes clicks).
    private readonly bool _layered = !Program.Settings.Experiment("not-layered");
    private readonly Uri _layoutUrl;
    private Composition? _composition;
    private CoreWebView2CompositionController? _controller;

    public OverlayWindow(Uri layoutUrl)
    {
        _layoutUrl = layoutUrl;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(0, 0, 400, 300);
        Text = "stgadgets overlay";
        CreateHandle();
        _ = InitializeAsync();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate | WsExNoRedirectionBitmap;
            if (_layered)
                parameters.ExStyle |= WsExLayered | WsExTransparent;
            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_layered)
            _ = SetLayeredWindowAttributes(Handle, 0, 255, LwaAlpha);
    }

    // Nothing to paint: there's no redirection bitmap, and WebView2 draws everything.
    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_controller is not null)
            _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Keep rendering at full rate while another app is active, which is all the time (docs/requirements.md, L2).
            var options = new CoreWebView2EnvironmentOptions(
                "--disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows");
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "stgadgets", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userData, options);

            _composition = new Composition(Handle);
            _controller = await environment.CreateCoreWebView2CompositionControllerAsync(Handle);
            _controller.DefaultBackgroundColor = Color.Transparent;
            _controller.RootVisualTarget = _composition.RootVisual;
            _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
            _controller.IsVisible = true;
            _composition.Commit();

            // Retried every second while the server isn't up, or has no layouts to serve; only changes are logged.
            string? lastFailure = null;
            _controller.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                if (args.IsSuccess)
                {
                    lastFailure = null;
                    return;
                }

                // WebView2 counts an HTTP error as a failed navigation too, with no WebErrorStatus of its own.
                var failure = args.HttpStatusCode >= 400 ? $"HTTP {args.HttpStatusCode}" : args.WebErrorStatus.ToString();
                if (failure != lastFailure)
                    Console.WriteLine($"stgadgets overlay: couldn't load {_layoutUrl} ({failure}); retrying every second");
                lastFailure = failure;
                await Task.Delay(RetryDelay);
                _controller.CoreWebView2.Navigate(_layoutUrl.ToString());
            };
            _controller.CoreWebView2.Navigate(_layoutUrl.ToString());
        }
        catch (Exception ex) when (ex is COMException or WebView2RuntimeNotFoundException)
        {
            Console.WriteLine($"stgadgets overlay: couldn't start WebView2: {ex.Message}");
        }
    }

    void IOverlayWindow.ShowAt(ScreenRect frame) =>
        _ = SetWindowPos(Handle, HwndTopmost, (int)frame.X, (int)frame.Y, (int)frame.Width, (int)frame.Height, SwpNoActivate | SwpShowWindow);

    void IOverlayWindow.Hide() => _ = ShowWindow(Handle, SwHide);

    public void Reload() => _controller?.CoreWebView2.Reload();

    public void OpenDevTools() => _controller?.CoreWebView2.OpenDevToolsWindow();

    /// <summary>Outlines the layout in red, to check the overlay lines up with the emulator. Until the next reload.</summary>
    public void ShowBounds(bool show) => _ = _controller?.CoreWebView2.ExecuteScriptAsync(
        $"document.documentElement.style.outline = '{(show ? "2px solid red" : "")}'; document.documentElement.style.outlineOffset = '-2px';");

    /// <summary>
    /// A snapshot of what WebView2 is drawing, for SnapshotServer; null if it couldn't take one. A hidden overlay
    /// (the emulator isn't the active app) is resized to <paramref name="hiddenFrame"/>, the emulator's content area,
    /// first, so the snapshot shows the layout at the size it would have.
    /// </summary>
    /// <param name="hiddenFrame">The emulator's content area, if it has one; called on the UI thread.</param>
    /// <param name="width">Width in pixels; null for the overlay's own size.</param>
    /// <param name="restored">Called on the UI thread after a hidden overlay's snapshot, so its owner can put it back.</param>
    public Task<OverlaySnapshot?> SnapshotAsync(Func<ScreenRect?> hiddenFrame, int? width, Action restored) => OnUiThread(async () =>
    {
        if (_controller is null)
            return null;

        var visible = IsWindowVisible(Handle) != 0;
        if (!visible && hiddenFrame() is { } frame)
        {
            _ = SetWindowPos(Handle, 0, (int)frame.X, (int)frame.Y, (int)frame.Width, (int)frame.Height, SwpNoActivate | SwpNoZOrder);
            await Task.Delay(HiddenSnapshotDelay);
        }

        using var png = new MemoryStream();
        await _controller.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        if (!visible)
            restored();
        return new OverlaySnapshot(Scale(png, width), visible);
    });

    private static byte[] Scale(MemoryStream png, int? width)
    {
        if (width is not { } w)
            return png.ToArray();

        png.Position = 0;
        using var image = Image.FromStream(png);
        var height = Math.Max(1, (int)Math.Round(w * (double)image.Height / image.Width));
        using var scaled = new Bitmap(w, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(scaled))
            graphics.DrawImage(image, 0, 0, w, height);
        using var result = new MemoryStream();
        scaled.Save(result, ImageFormat.Png);
        return result.ToArray();
    }

    private Task<T> OnUiThread<T>(Func<Task<T>> action)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvoke(async () =>
        {
            try
            {
                done.SetResult(await action());
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
            {
                done.SetException(ex);
            }
        });
        return done.Task;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller?.Close();
            _composition?.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll")]
    private static extern int SetLayeredWindowAttributes(nint window, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern int SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern int ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    private static extern int IsWindowVisible(nint window);
}
