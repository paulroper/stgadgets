using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Stgadgets.Cli.Peek;

namespace Stgadgets.Cli.Tests.Peek;

/// <summary><c>stgadgets peek values</c>, against a fake server that answers with set messages.</summary>
public sealed class ValuesCommandTests : IDisposable
{
    private const string Connected = """{ "state": "connected", "pack": "test", "ticks": 10, "provider": "retroarch" }""";
    private const string Hello = """
        { "type": "hello", "pack": "test", "values": {
            "p1.score": 1500, "p1.active": true, "rank": 0.25, "stage.name": "Cave", "rank.reason": null,
            "enemies": [{ "hp": 3 }, { "hp": 4 }] } }
        """;

    private readonly FakeServer _server = new();

    public void Dispose() => _server.Dispose();

    private (int ExitCode, string Output, string Error) Peek(params string[] args)
    {
        var root = new RootCommand { PeekCommand.Create(server: _server.Url) };
        var (output, error) = (new StringWriter(), new StringWriter());
        var exitCode = root.Parse(["peek", "values", .. args]).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exitCode, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Shows_the_latest_values_from_the_servers_hello()
    {
        _server.Status = Connected;
        _server.Messages = ["""{ "type": "status", "state": "connected" }""", Hello];

        Assert.Equal(
            (0, "p1.score     1500\np1.active    true\nrank         0.25\nstage.name   'Cave'\nrank.reason  null\n", ""),
            Peek());
    }

    [Fact]
    public void Shows_the_values_asked_for()
    {
        _server.Status = Connected;
        _server.Messages = [Hello];

        Assert.Equal((0, "enemies[0].hp  3\nenemies[1].hp  4\np1.score       1500\n", ""), Peek("enemies", "p1.score"));
    }

    [Fact]
    public void Waits_for_the_first_tick_when_the_server_has_just_found_the_game()
    {
        _server.Status = Connected;
        _server.Messages =
        [
            """{ "type": "hello", "pack": "test", "values": {} }""",
            """{ "type": "tick", "seq": 1, "changed": { "p1.score": 0 } }""",
        ];

        Assert.Equal((0, "p1.score  0\n", ""), Peek());
    }

    [Fact]
    public void Says_when_the_server_isnt_running()
    {
        _server.Dispose();

        Assert.Equal((1, "", $"The server isn't running at {_server.Url}: start it with `stgadgets dev start`\n"), Peek());
    }

    [Fact]
    public void Wont_read_a_replay()
    {
        _server.Status = """{ "state": "connected", "pack": "test", "ticks": 10, "provider": "replay" }""";

        Assert.StartsWith("The server is replaying a recording; peek reads live play.", Peek().Error);
    }

    [Fact]
    public void Says_when_the_server_isnt_following_a_game()
    {
        _server.Status = """{ "state": "no-pack", "detail": "No pack for 'other'", "ticks": 0, "provider": "retroarch" }""";

        Assert.Equal((1, "", "The server isn't following a game: no-pack, No pack for 'other'\n"), Peek());
    }

    [Fact]
    public void Gives_up_when_no_values_come()
    {
        _server.Status = Connected;
        _server.Messages = ["""{ "type": "status", "state": "connected" }"""];

        Assert.Equal((1, "", "The server sent no values within 2 s\n"), Peek());
    }

    [Fact]
    public void Says_when_a_value_isnt_published()
    {
        _server.Status = Connected;
        _server.Messages = [Hello];

        Assert.Equal((1, "", "test has no 'p2.score'\n"), Peek("p2.score"));
    }

    /// <summary>
    /// Answers <c>GET /api/status</c> with <see cref="Status"/>, and sends <see cref="Messages"/> to each WebSocket
    /// client on /ws, then keeps the connection open, as the server does between ticks.
    /// </summary>
    private sealed class FakeServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly TaskCompletionSource _stopped = new();

        public FakeServer()
        {
            Url = new Uri($"http://127.0.0.1:{FreePort()}/");
            _listener.Prefixes.Add(Url.ToString());
            _listener.Start();
            _ = ServeAsync();
        }

        public Uri Url { get; }

        public string Status { get; set; } = "{}";

        public string[] Messages { get; set; } = [];

        public void Dispose()
        {
            _stopped.TrySetResult();
            if (_listener.IsListening)
                _listener.Close();
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
                    return;
                }
                _ = context.Request.IsWebSocketRequest ? SendMessagesAsync(context) : SendStatusAsync(context);
            }
        }

        private async Task SendStatusAsync(HttpListenerContext context)
        {
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(Status));
            context.Response.Close();
        }

        private async Task SendMessagesAsync(HttpListenerContext context)
        {
            var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
            foreach (var message in Messages)
                await socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            await _stopped.Task;
        }

        private static int FreePort()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
    }
}
