using System.Net;

namespace Stgadgets.Host.Lib.Overlay;

/// <summary>A PNG of what the overlay is drawing, and whether the overlay was on screen when it was taken.</summary>
public sealed record OverlaySnapshot(byte[] Png, bool Visible);

/// <summary>
/// Development hook: GET http://127.0.0.1:{port}/snapshot returns a PNG of what the overlay is drawing (the layout
/// only, on a transparent background, not the game underneath), so a layout can be checked without looking at the
/// screen. ?width=N scales it to N pixels wide. The X-Overlay-Visible header says whether the overlay was showing:
/// a hidden overlay is still drawn at the emulator's size. <c>stgadgets dev snapshot</c> (src/cli) relies on all of
/// this, so every host serves it the same way. Off unless STGADGETS_SNAPSHOT_PORT is set (<see cref="OverlaySettings"/>).
/// </summary>
public sealed class SnapshotServer : IDisposable
{
    private const int MaxWidth = 8192;

    private readonly HttpListener _listener = new();
    private readonly Func<int?, Task<OverlaySnapshot?>> _capture;

    /// <param name="capture">Takes a snapshot, given the width asked for (null: the host's default size); null if the
    /// host can't take one right now. Called on a thread-pool thread.</param>
    public SnapshotServer(int port, Func<int?, Task<OverlaySnapshot?>> capture)
    {
        _capture = capture;
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

        int? width = int.TryParse(context.Request.QueryString["width"], out var w) && w is > 0 and <= MaxWidth ? w : null;
        if (await _capture(width) is not { } snapshot)
        {
            response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            return;
        }

        response.Headers["X-Overlay-Visible"] = snapshot.Visible ? "true" : "false";
        response.ContentType = "image/png";
        await response.OutputStream.WriteAsync(snapshot.Png);
    }

    public void Dispose() => _listener.Close();
}
