using Stgadgets.Core.Maps;
using Stgadgets.Core.Providers;

namespace Stgadgets.Server.GameState;

/// <summary>
/// The main loop: find what's running, pick its map, then decode and publish every snapshot the provider pushes
/// (docs/architecture.md#the-loop).
/// </summary>
public sealed class GameStateService(
    IEmulatorProvider provider,
    MapCatalog maps,
    VariableBroadcaster broadcaster,
    ILogger<GameStateService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Loaded {Count} memory map(s): {Ids}", maps.Maps.Count, string.Join(", ", maps.Maps.Select(m => m.Game.Id)));

        string? lastWarning = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                lastWarning = null;
            }
            catch (Exception ex) when (ex is TimeoutException or NotSupportedException)
            {
                broadcaster.SetStatus("no-emulator", ex.Message);
                if (ex.Message != lastWarning)   // retried every second; only log changes
                    logger.LogWarning("{Provider}: {Message}", provider.Id, ex.Message);
                lastWarning = ex.Message;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never let a provider error stop the app; log it and reconnect.
                broadcaster.SetStatus("no-emulator", ex.Message);
                logger.LogError(ex, "{Provider} failed; retrying", provider.Id);
            }

            await Task.Delay(RetryDelay, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var content = await provider.GetContentAsync(cancellationToken);
        if (content is null)
        {
            broadcaster.SetStatus("no-content", "No game loaded");
            return;
        }

        var map = maps.Find(content);
        if (map is null)
        {
            broadcaster.SetStatus("no-map", $"No memory map for '{content.Romset}'");
            return;
        }

        var decoder = new MapDecoder(map);
        logger.LogInformation("Playing {Game} ({Romset} on {System}); reading {Ranges}",
            map.Game.Title, content.Romset, content.System, string.Join(", ", decoder.Plan.Ranges));
        broadcaster.SetGame(map, content.Romset);
        broadcaster.SetStatus("connected");

        await foreach (var snapshot in provider.StreamAsync(content, decoder.Plan, cancellationToken))
        {
            var readAt = VariableBroadcaster.UnixMs();
            broadcaster.PublishTick(decoder.Decode(snapshot), readAt);
        }

        logger.LogInformation("Content changed or emulator went away");
    }
}
