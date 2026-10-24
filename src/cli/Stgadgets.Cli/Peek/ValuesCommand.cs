using System.CommandLine;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Stgadgets.Cli.Dev;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;

namespace Stgadgets.Cli.Peek;

/// <summary>
/// <c>stgadgets peek values</c>: the latest values the running server published, map and derived alike, as gadgets
/// see them. Derived values build up over play, so only the server, which has run derived.js on every tick since the
/// game started, has them right.
/// </summary>
internal static class ValuesCommand
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <param name="server">The server to ask; tests run their own.</param>
    public static Command Create(Uri? server = null)
    {
        server ??= new Uri($"http://127.0.0.1:{DevStack.ServerPort}");

        var names = PeekCommand.Names("Values");
        var command = new Command(
            "values",
            "The latest values the running server published, map and derived.js alike. Start it with `stgadgets dev start`.")
        {
            names,
        };
        command.SetAction(StgadgetsCommand.HandleAsync(async (result, cancellationToken) =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            try
            {
                var status = await StatusAsync(server, timeout.Token);
                if (status.Provider == "replay")
                    throw new UsageException("The server is replaying a recording; peek reads live play. For a recording, use `stgadgets recording values`");
                if (status.State != "connected")
                    throw new UsageException($"The server isn't following a game: {status.State}{(status.Detail is null ? "" : $", {status.Detail}")}");

                var (pack, values) = await LatestValuesAsync(server, timeout.Token);
                PeekCommand.Write(values, result.GetValue(names) ?? [], pack, result.InvocationConfiguration.Output);
                foreach (var warning in DevStack.Warnings(status))
                    result.InvocationConfiguration.Error.WriteLine($"warning: {warning}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The server sent no values within {Timeout.TotalSeconds:F0} s");
            }
            catch (WebSocketException ex)
            {
                throw new UsageException($"Lost the connection to the server: {ex.Message}");
            }
        }));
        return command;
    }

    private static async Task<ServerStatus> StatusAsync(Uri server, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        try
        {
            return await http.GetFromJsonAsync<ServerStatus>(new Uri(server, "/api/status"), JsonSerializerOptions.Web, cancellationToken)
                ?? throw new UsageException("The server sent an empty status");
        }
        catch (HttpRequestException)
        {
            throw new UsageException($"The server isn't running at {server}: start it with `stgadgets dev start`");
        }
    }

    /// <summary>
    /// The values from the server's hello, which has the latest of each. Just after it finds a game they can still be
    /// empty, so then the first tick, which has them all, fills them in.
    /// </summary>
    private static async Task<(string Pack, Dictionary<string, object?> Values)> LatestValuesAsync(Uri server, CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new UriBuilder(server) { Scheme = "ws", Path = "/ws" }.Uri, cancellationToken);
        string? pack = null;
        while (true)
        {
            using var message = await ReceiveAsync(socket, cancellationToken);
            var root = message.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "hello":
                    pack = root.GetProperty("pack").GetString();
                    if (Values(root.GetProperty("values")) is { Count: > 0 } values)
                        return (pack!, values);
                    break;
                case "tick" when pack is not null:
                    return (pack, Values(root.GetProperty("changed")));
            }
        }
    }

    private static async Task<JsonDocument> ReceiveAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        WebSocketReceiveResult received;
        do
        {
            received = await socket.ReceiveAsync(chunk, cancellationToken);
            if (received.MessageType == WebSocketMessageType.Close)
                throw new UsageException("The server closed the connection");
            buffer.Write(chunk, 0, received.Count);
        }
        while (!received.EndOfMessage);
        return JsonDocument.Parse(buffer.ToArray());
    }

    private static Dictionary<string, object?> Values(JsonElement values) =>
        values.EnumerateObject().ToDictionary(v => v.Name, v => Value(v.Value));

    /// <summary>A value as the decoder and derived.js make them (<see cref="PackYaml.ParseValue"/>), so it prints the same.</summary>
    private static object? Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetInt64(out var whole) ? whole : value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Array when value.EnumerateArray().All(e => e.ValueKind == JsonValueKind.Object) =>
            new ArrayValue([.. value.EnumerateArray().Select(entry => (IReadOnlyDictionary<string, object?>)Values(entry))]),
        JsonValueKind.Null => null,
        _ => value.GetRawText(),
    };
}
