using AppKit;
using CoreGraphics;
using Foundation;
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
