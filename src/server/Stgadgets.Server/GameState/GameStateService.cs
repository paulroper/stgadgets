using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;

namespace Stgadgets.Server.GameState;

/// <summary>
/// The main loop: find what's running, pick its pack, then decode and publish every snapshot the provider pushes
/// (docs/architecture.md#the-loop).
/// </summary>
public sealed class GameStateService(
    IEmulatorProvider provider,
    PackCatalog packs,
    VariableBroadcaster broadcaster,
    ILogger<GameStateService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Loaded {Count} pack(s): {Ids}", packs.Packs.Count, string.Join(", ", packs.Packs.Select(p => p.Id)));

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

        var pack = packs.Find(content);
        if (pack is null)
        {
            broadcaster.SetStatus("no-pack", $"No pack for '{content.Romset}'");
            return;
        }

        var decoder = new MapDecoder(pack.Map);
        logger.LogInformation("Playing {Game} (pack {Pack}, {Romset} on {System}); reading {Ranges}",
            pack.Game.Title, pack.Id, content.Romset, content.System, string.Join(", ", decoder.Plan.Ranges));
        broadcaster.SetGame(pack, content.Romset);
        broadcaster.SetStatus("connected");

        var settings = pack.ProviderSettings.GetValueOrDefault(provider.Id);
        await foreach (var snapshot in provider.StreamAsync(content, decoder.Plan, settings, cancellationToken))
        {
            var readAt = VariableBroadcaster.UnixMs();
            broadcaster.PublishTick(decoder.Decode(snapshot), readAt);
        }

        logger.LogInformation("Content changed or emulator went away");
    }
}
