using System.Net;
using System.Net.Http.Json;

namespace Stgadgets.Host.Lib.Api;

/// <summary>The emulator the stgadgets server is connected to (GET /api/emulator).</summary>
public sealed record EmulatorTarget(string Provider, string AppName, int? ProcessId);

/// <summary>Asks the stgadgets server which emulator to follow, so the overlay isn't tied to one emulator.</summary>
public sealed class EmulatorLocator : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly HttpClient _http;
    private readonly CancellationTokenSource _stop = new();
    private volatile EmulatorTarget? _target;
    private volatile bool _serverReachable;

    public EmulatorLocator(Uri server)
    {
        _http = new HttpClient { BaseAddress = server, Timeout = TimeSpan.FromSeconds(1) };
        _ = PollAsync(_stop.Token);
    }

    /// <summary>The emulator to follow, or null if the server isn't connected to one.</summary>
    public EmulatorTarget? Target => _target;

    public bool ServerReachable => _serverReachable;

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var response = await _http.GetAsync("api/emulator", cancellationToken);
                _target = response.StatusCode == HttpStatusCode.OK
                    ? await response.Content.ReadFromJsonAsync<EmulatorTarget>(cancellationToken)
                    : null;
                _serverReachable = true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _target = null;
                _serverReachable = false;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _http.Dispose();
    }
}
