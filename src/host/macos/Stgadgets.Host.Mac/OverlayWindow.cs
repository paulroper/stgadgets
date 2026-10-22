using WebKit;

namespace Stgadgets.Host.Mac;

/// <summary>
/// A borderless, transparent, click-through window that floats above the emulator and shows a layout in a WKWebView.
/// See docs/decisions/0007-native-overlay-host.md.
/// </summary>
public sealed class OverlayWindow : NSWindow
{
    private readonly WKWebView _webView;

    public OverlayWindow(NSUrl layoutUrl)
        : base(new CGRect(0, 0, 400, 300), NSWindowStyle.Borderless, NSBackingStore.Buffered, deferCreation: false)
    {
        IsOpaque = false;
        BackgroundColor = NSColor.Clear;
        HasShadow = false;
        IgnoresMouseEvents = !AppDelegate.Experiment("keyable");   // clicks go to the emulator underneath
        Level = AppDelegate.Experiment("normal-level") ? NSWindowLevel.Normal : NSWindowLevel.Floating;   // above normal app windows
        CollectionBehavior =
            NSWindowCollectionBehavior.CanJoinAllSpaces
            | NSWindowCollectionBehavior.FullScreenAuxiliary   // allowed over fullscreen Spaces
            | NSWindowCollectionBehavior.Stationary
            | NSWindowCollectionBehavior.IgnoresCycle;          // not in Cmd-` window cycling

        _webView = new WKWebView(ContentView!.Bounds, new WKWebViewConfiguration())
        {
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
        };
        // No public API for a transparent WKWebView on macOS; this KVC switch is the widely used approach.
        _webView.SetValueForKey(NSNumber.FromBoolean(false), new NSString("drawsBackground"));
        _webView.Inspectable = true;   // Safari → Develop → <this Mac> → stgadgets
        _webView.NavigationDelegate = new RetryOnFailure(layoutUrl);
        ContentView.AddSubview(_webView);
        _webView.LoadRequest(new NSUrlRequest(layoutUrl));
    }

    // Never take focus from the emulator.
    public override bool CanBecomeKeyWindow => AppDelegate.Experiment("keyable");
    public override bool CanBecomeMainWindow => AppDelegate.Experiment("keyable");

    public void Reload() => _webView.Reload();

    // WebKit doesn't draw a window that isn't on screen, so a hidden overlay is shown fully transparent for this long
    // before its snapshot: enough to lay out at the new size and paint.
    private static readonly TimeSpan HiddenSnapshotDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// A PNG of what the webview is drawing, and whether the overlay was on screen. For SnapshotServer. A hidden
    /// overlay (the emulator isn't the active app) is drawn invisibly at <paramref name="hiddenFrame"/>, the
    /// emulator's content area, so the snapshot shows the layout at the size it would have.
    /// </summary>
    /// <param name="width">Width in pixels; null for one pixel per point (half a Retina screen's pixels).</param>
    /// <param name="restored">Called on the main thread once a hidden overlay is hidden again, so its owner can
    /// re-check whether it should be showing (the emulator may have become active meanwhile).</param>
    public async Task<(NSData? Png, bool Visible)> SnapshotAsync(CGRect? hiddenFrame, int? width, Action restored)
    {
        var visible = await OnMainThread(() => IsVisible);
        if (!visible)
        {
            await OnMainThread(() =>
            {
                if (hiddenFrame is { } frame)
                    SetFrame(frame, display: true);
                AlphaValue = 0;
                OrderFrontRegardless();
                return true;
            });
            await Task.Delay(HiddenSnapshotDelay);
        }

        var done = new TaskCompletionSource<NSData?>();
        BeginInvokeOnMainThread(() => _webView.TakeSnapshot(new WKSnapshotConfiguration(), (image, _) =>
            done.TrySetResult(image is null ? null : ToPng(image, width ?? (int)image.Size.Width))));
        var png = await done.Task;

        if (!visible)
        {
            await OnMainThread(() =>
            {
                OrderOut(null);
                AlphaValue = 1;
                restored();
                return true;
            });
        }
        return (png, visible);
    }

    /// <summary>Draws <paramref name="image"/> into a bitmap <paramref name="width"/> pixels wide, keeping its aspect.</summary>
    private static NSData? ToPng(NSImage image, int width)
    {
        var height = (int)Math.Round(width * image.Size.Height / image.Size.Width);
        if (width <= 0 || height <= 0)
            return null;

        var bitmap = new NSBitmapImageRep(IntPtr.Zero, width, height, 8, 4, true, false, NSColorSpace.DeviceRGB, width * 4, 32);
        NSGraphicsContext.GlobalSaveGraphicsState();
        NSGraphicsContext.CurrentContext = NSGraphicsContext.FromBitmap(bitmap);
        image.Draw(new CGRect(0, 0, width, height), CGRect.Empty, NSCompositingOperation.Copy, 1);
        NSGraphicsContext.GlobalRestoreGraphicsState();
        return bitmap.RepresentationUsingTypeProperties(NSBitmapImageFileType.Png);
    }

    private Task<T> OnMainThread<T>(Func<T> action)
    {
        var done = new TaskCompletionSource<T>();
        BeginInvokeOnMainThread(() => done.TrySetResult(action()));
        return done.Task;
    }

    /// <summary>Draws a red outline around the overlay, to check it lines up with the emulator.</summary>
    public bool ShowBounds
    {
        get => ContentView!.Layer?.BorderWidth > 0;
        set
        {
            ContentView!.WantsLayer = true;
            ContentView.Layer!.BorderColor = NSColor.SystemRed.CGColor;
            ContentView.Layer.BorderWidth = value ? 2 : 0;
        }
    }

    /// <summary>Keeps retrying the layout URL, e.g. when the overlay starts before the stgadgets server.</summary>
    private sealed class RetryOnFailure(NSUrl url) : WKNavigationDelegate
    {
        private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

        public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error) =>
            Retry(webView, error);

        public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error) =>
            Retry(webView, error);

        private void Retry(WKWebView webView, NSError error)
        {
            Console.WriteLine($"stgadgets overlay: couldn't load {url} ({error.LocalizedDescription}); retrying");
            NSTimer.CreateScheduledTimer(Delay, _ => webView.LoadRequest(new NSUrlRequest(url)));
        }
    }
}
