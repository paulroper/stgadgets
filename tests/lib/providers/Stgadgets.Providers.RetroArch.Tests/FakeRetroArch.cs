using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Stgadgets.Providers.RetroArch.Tests;

/// <summary>
/// Answers RetroArch's network commands on a loopback port: GET_STATUS with a game, and READ_CORE_RAM with zeros.
/// While <see cref="Stalled"/>, it answers nothing, as RetroArch doesn't while its window is being dragged on Windows.
/// </summary>
internal sealed class FakeRetroArch : IDisposable
{
    private readonly UdpClient _udp = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();

    public FakeRetroArch(string status)
    {
        Status = status;
        _ = ServeAsync();
    }

    public IPEndPoint Endpoint => (IPEndPoint)_udp.Client.LocalEndPoint!;

    /// <summary>What follows "GET_STATUS " in the reply, such as "PLAYING fb_alpha,batriderj,crc32=6478f475".</summary>
    public string Status { get; set; }

    public bool Stalled { get; set; }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            UdpReceiveResult request;
            try
            {
                request = await _udp.ReceiveAsync(_stop.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            if (Stalled)
                continue;

            var reply = Reply(Encoding.ASCII.GetString(request.Buffer).Split(' '));
            await _udp.SendAsync(Encoding.ASCII.GetBytes(reply), request.RemoteEndPoint, _stop.Token);
        }
    }

    // "READ_CORE_RAM 100 16" → "READ_CORE_RAM 100 00 00 …"
    private string Reply(string[] command) => command switch
    {
        ["GET_STATUS"] => $"GET_STATUS {Status}",
        ["READ_CORE_RAM", var offset, var length] =>
            $"READ_CORE_RAM {offset} {string.Join(' ', Enumerable.Repeat("00", int.Parse(length, CultureInfo.InvariantCulture)))}",
        _ => "",
    };

    public void Dispose()
    {
        _stop.Cancel();
        _udp.Dispose();
        _stop.Dispose();
    }
}
