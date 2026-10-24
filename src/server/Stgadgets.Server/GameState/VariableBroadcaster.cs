using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Stgadgets.Core.Packs;

namespace Stgadgets.Server.GameState;

/// <summary>
/// What the server is doing, for <c>GET /api/status</c>: the connection state, the matched pack, ticks sent, the
/// provider reading the game, so tools can tell live play from a replay, and why the latest pack reload failed.
/// </summary>
public sealed record ServerStatus(string State, string? Detail, string? Pack, long Ticks, string? Provider = null, string? PackError = null);

/// <summary>
/// Sends game state to connected layouts over WebSocket. Every tick is delivered, in order, to every client
/// (docs/gadgets.md#wire-protocol-internal).
/// </summary>
public sealed class VariableBroadcaster(ILogger<VariableBroadcaster> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<Channel<byte[]>, byte> _clients = new();

    // Current state, so new clients start with a full picture.
    private object _status = new { type = "status", state = "starting" };
    private (string State, string? Detail) _state = ("starting", null);
    private string? _pack;
    private object? _game;
    private object? _variables;
    private Dictionary<string, object?> _values = [];
    private long _seq;

    public void SetStatus(string state, string? detail = null)
    {
        lock (_gate)
        {
            _status = new { type = "status", state, detail };
            _state = (state, detail);
            Broadcast(_status);
        }
    }

    public void SetGame(Pack pack, string? romset)
    {
        lock (_gate)
        {
            _pack = pack.Id;
            _game = new { pack.Game.Title, pack.Game.Manufacturer, pack.Game.Year, romset, pack.Game.Screen };
            _variables = pack.Map.Variables
                .Select(v => (v.Name, Info: (object)new { type = v.Type.ToString(), v.Description }))
                .Concat(pack.Map.Arrays.Select(a => (a.Name, Info: (object)new
                {
                    type = "array",
                    a.Description,
                    fields = a.Fields.ToDictionary(f => f.Name, f => new { type = f.Type.ToString(), f.Description }),
                })))
                .Concat((pack.Derived?.Values ?? []).Select(d => (d.Name, Info: (object)new { type = "derived", d.Description })))
                .ToDictionary(v => v.Name, v => v.Info);
            _values = [];
            Broadcast(Hello());
        }
    }

    /// <param name="readAt">When the provider received the memory (Unix ms), for latency measurement.</param>
    public void PublishTick(Dictionary<string, object?> values, double readAt)
    {
        lock (_gate)
        {
            var changed = values
                .Where(kv => !_values.TryGetValue(kv.Key, out var old) || !Equals(old, kv.Value))
                .ToDictionary();
            _values = values;
            Broadcast(new { type = "tick", seq = ++_seq, readAt, sentAt = UnixMs(), changed });
        }
    }

    public async Task RunClientAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var outbox = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
        lock (_gate)
        {
            outbox.Writer.TryWrite(Serialize(_status));
            if (_game is not null)
                outbox.Writer.TryWrite(Serialize(Hello()));
            _clients.TryAdd(outbox, 0);
        }

        using var closed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ReceiveAsync(socket, closed);
        try
        {
            await foreach (var message in outbox.Reader.ReadAllAsync(closed.Token))
                await socket.SendAsync(message, WebSocketMessageType.Text, endOfMessage: true, closed.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // Client went away.
        }
        finally
        {
            _clients.TryRemove(outbox, out _);
        }
    }

    public ServerStatus Status()
    {
        lock (_gate)
            return new ServerStatus(_state.State, _state.Detail, _pack, _seq);
    }

    public static double UnixMs() => (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;

    private object Hello() => new { type = "hello", pack = _pack, game = _game, variables = _variables, values = _values };

    private void Broadcast(object message)
    {
        var bytes = Serialize(message);
        foreach (var client in _clients.Keys)
            client.Writer.TryWrite(bytes);
    }

    private static byte[] Serialize(object message) => JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

    /// <summary>Reads diagnostics from the client (latency reports) until it closes the connection.</summary>
    private async Task ReceiveAsync(WebSocket socket, CancellationTokenSource closed)
    {
        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, closed.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                if (result.EndOfMessage)
                    LogClientMessage(buffer.AsSpan(0, result.Count));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException) { }
        finally
        {
            await closed.CancelAsync();
        }
    }

    private void LogClientMessage(ReadOnlySpan<byte> json)
    {
        try
        {
            var report = JsonSerializer.Deserialize<LatencyReport>(json, JsonOptions);
            if (report?.Type == "latency")
            {
                logger.LogInformation(
                    "Latency ({Source}): read → paint p50 {P50:F1} ms, p95 {P95:F1} ms, max {Max:F1} ms at {Rate:F0} Hz ({Samples} ticks); p50 split: read → arrive {Arrive:F1} ms, arrive → paint {Paint:F1} ms; rAF {RafHz:F0} Hz, timer {TimerHz:F0} Hz, {Visibility}, focused {Focused}; " +
                    "rAF interval p50 {RafIntervalP50:F1} ms, p95 {RafIntervalP95:F1} ms, {RafLongShare:P0} long; frame work p50 {WorkP50:F1} ms, p95 {WorkP95:F1} ms, max {WorkMax:F1} ms",
                    report.Source, report.P50, report.P95, report.Max, report.Rate, report.Samples, report.ArriveP50, report.PaintP50,
                    report.RafHz, report.TimerHz, report.Visibility, report.Focused,
                    report.RafIntervalP50, report.RafIntervalP95, report.RafLongShare, report.WorkP50, report.WorkP95, report.WorkMax);
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Ignoring malformed client message");
        }
    }

    private sealed record LatencyReport(
        string Type, string Source, double P50, double P95, double Max, double Rate, int Samples, double ArriveP50, double PaintP50,
        double RafHz, double TimerHz, string? Visibility, bool Focused,
        double RafIntervalP50, double RafIntervalP95, double RafLongShare, double WorkP50, double WorkP95, double WorkMax);
}
