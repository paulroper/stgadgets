using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Stgadgets.Providers.RetroArch;

/// <param name="State">PLAYING, PAUSED or CONTENTLESS.</param>
/// <param name="System">RetroArch's system id for the core, e.g. "fb_alpha".</param>
/// <param name="Content">The content's file name without extension, e.g. "invaders".</param>
public sealed record RetroArchStatus(string State, string? System, string? Content);

/// <summary>
/// RetroArch's UDP network command interface (network_cmd_enable = "true").
/// RetroArch handles commands once per frame, so each round-trip takes about a frame.
/// </summary>
public sealed class RetroArchClient : IDisposable
{
    /// <summary>Larger replies exceed the maximum UDP datagram size on macOS (~9 KB, 3 hex chars per byte) and are dropped.</summary>
    public const int MaxReadLength = 2048;

    private readonly UdpClient _udp = new();
    private readonly TimeSpan _timeout;

    public RetroArchClient(IPEndPoint endpoint, TimeSpan timeout)
    {
        _udp.Connect(endpoint);
        _timeout = timeout;
    }

    public async Task<RetroArchStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await SendAsync("GET_STATUS", cancellationToken);

        // "GET_STATUS PLAYING fb_alpha,invaders,crc32=be3e15a" or "GET_STATUS CONTENTLESS".
        // Skip late replies to earlier, timed-out reads (RetroArch stalls while its menu is open).
        string[] parts;
        do
        {
            parts = (await ReceiveAsync(cancellationToken)).Split(' ', 3);
        } while (parts[0] != "GET_STATUS");

        if (parts is not [_, var state, ..])
            throw new InvalidDataException($"Unexpected GET_STATUS reply: {string.Join(' ', parts)}");
        var fields = parts.Length > 2 ? parts[2].Split(',') : [];
        return new RetroArchStatus(state, fields.ElementAtOrDefault(0), fields.ElementAtOrDefault(1));
    }

    /// <summary>
    /// Reads core RAM with READ_CORE_RAM (RetroAchievements addressing). All requests are sent before waiting for
    /// replies, so a batch costs about one frame rather than one frame per request.
    /// </summary>
    public async Task ReadCoreRamAsync(IReadOnlyList<(uint Offset, Memory<byte> Destination)> reads, CancellationToken cancellationToken)
    {
        var pending = new Dictionary<uint, Memory<byte>>(reads.Count);
        foreach (var (offset, destination) in reads)
        {
            if (destination.Length > MaxReadLength)
                throw new ArgumentException($"Reads are limited to {MaxReadLength} bytes", nameof(reads));
            pending[offset] = destination;
            await SendAsync($"READ_CORE_RAM {offset:x} {destination.Length}", cancellationToken);
        }

        while (pending.Count > 0)
        {
            // "READ_CORE_RAM 20f8 10 00" or "READ_CORE_RAM 20f8 -1"
            var parts = (await ReceiveAsync(cancellationToken)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts is not ["READ_CORE_RAM", var address, ..]
                || !uint.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset)
                || !pending.Remove(offset, out var destination))
            {
                continue;   // a late reply to an earlier, timed-out request
            }

            if (parts.Length > 2 && parts[2] == "-1")
                throw new InvalidOperationException($"RetroArch couldn't read core RAM at 0x{offset:x}: {string.Join(' ', parts[3..])}");
            if (parts.Length - 2 != destination.Length)
                throw new InvalidDataException($"Asked for {destination.Length} bytes at 0x{offset:x}, got {parts.Length - 2}");

            var span = destination.Span;
            for (var i = 0; i < span.Length; i++)
                span[i] = byte.Parse(parts[i + 2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    private async Task SendAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            await _udp.SendAsync(Encoding.ASCII.GetBytes(command), cancellationToken);
        }
        // A refusal for an earlier datagram is reported by whichever call comes next, often a send.
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            throw NotListening(ex);
        }
    }

    private async Task<string> ReceiveAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            var result = await _udp.ReceiveAsync(timeout.Token);
            return Encoding.ASCII.GetString(result.Buffer).TrimEnd('\n', '\0');
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("No reply from RetroArch. Is it running with network_cmd_enable = \"true\"?");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            throw NotListening(ex);
        }
    }

    /// <summary>
    /// A timeout, so callers retry and log it as they do a missing reply: RetroArch stops listening briefly while it
    /// loads content.
    /// </summary>
    private static TimeoutException NotListening(SocketException ex) =>
        new("RetroArch isn't listening for network commands.", ex);

    public void Dispose() => _udp.Dispose();
}
