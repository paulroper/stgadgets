using System.Net;

namespace Stgadgets.Host.Mac;

/// <summary>
/// Development hook: GET http://127.0.0.1:{port}/snapshot returns a PNG of what the overlay is drawing (the layout
/// only, on a transparent background, not the game underneath), so a layout can be checked without looking at the
/// screen. One pixel per point by default; ?width=N scales it to N pixels wide. Off unless STGADGETS_SNAPSHOT_PORT
/// is set.
/// </summary>
public sealed class SnapshotServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly OverlayWindow _overlay;
    private readonly Func<CGRect?> _emulatorFrame;
    private readonly Action _restored;

    /// <param name="emulatorFrame">The emulator's content area, to draw a hidden overlay at; called on the main thread.</param>
    /// <param name="restored">Called on the main thread after a hidden overlay's snapshot.</param>
    public SnapshotServer(int port, OverlayWindow overlay, Func<CGRect?> emulatorFrame, Action restored)
    {
        _overlay = overlay;
        _emulatorFrame = emulatorFrame;
        _restored = restored;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _ = ServeAsync();
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;   // stopped
            }
            _ = RespondAsync(context);
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        using var response = context.Response;
        if (context.Request.HttpMethod != "GET" || context.Request.Url?.AbsolutePath != "/snapshot")
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        int? width = int.TryParse(context.Request.QueryString["width"], out var w) && w is > 0 and <= 8192 ? w : null;
        var frame = await MainThread(_emulatorFrame);
        var (png, visible) = await _overlay.SnapshotAsync(frame, width, _restored);
        if (png is null)
        {
            response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            return;
        }

        // Hidden while the emulator isn't the active app; the snapshot is still drawn at the emulator's size.
        response.Headers["X-Overlay-Visible"] = visible ? "true" : "false";
        response.ContentType = "image/png";
        var bytes = png.ToArray();
        await response.OutputStream.WriteAsync(bytes);
    }

    private Task<T> MainThread<T>(Func<T> action)
    {
        var done = new TaskCompletionSource<T>();
        _overlay.BeginInvokeOnMainThread(() => done.TrySetResult(action()));
        return done.Task;
    }

    public void Dispose() => _listener.Close();
}
