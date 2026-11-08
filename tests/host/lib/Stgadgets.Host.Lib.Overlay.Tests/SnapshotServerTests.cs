using System.Net;
using System.Net.Sockets;

namespace Stgadgets.Host.Lib.Overlay.Tests;

public sealed class SnapshotServerTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    private readonly HttpClient _http = new();
    private readonly int _port = FreePort();
    private readonly List<int?> _widths = [];
    private OverlaySnapshot? _next = new(Png, Visible: true);
    private readonly SnapshotServer _server;

    public SnapshotServerTests() => _server = new SnapshotServer(_port, width =>
    {
        _widths.Add(width);
        return Task.FromResult<OverlaySnapshot?>(_next);
    });

    private Uri Url(string pathAndQuery) => new($"http://127.0.0.1:{_port}{pathAndQuery}");

    [Fact]
    public async Task ServesTheSnapshotAsAPng()
    {
        using var response = await _http.GetAsync(Url("/snapshot"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["true"], response.Headers.GetValues("X-Overlay-Visible"));
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal([null], _widths);
    }

    [Fact]
    public async Task SaysWhenTheOverlayWasHidden()
    {
        _next = new OverlaySnapshot(Png, Visible: false);

        using var response = await _http.GetAsync(Url("/snapshot"));

        Assert.Equal(["false"], response.Headers.GetValues("X-Overlay-Visible"));
    }

    [Theory]
    [InlineData("?width=320", 320)]
    [InlineData("?width=0", null)]
    [InlineData("?width=9000", null)]
    [InlineData("?width=wide", null)]
    public async Task PassesOnAValidWidth(string query, int? width)
    {
        using var response = await _http.GetAsync(Url($"/snapshot{query}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([width], _widths);
    }

    [Fact]
    public async Task IsUnavailableWhenTheHostCannotTakeOne()
    {
        _next = null;

        using var response = await _http.GetAsync(Url("/snapshot"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ServesNothingElse()
    {
        using var response = await _http.GetAsync(Url("/other"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_widths);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        _server.Dispose();
        _http.Dispose();
    }
}
