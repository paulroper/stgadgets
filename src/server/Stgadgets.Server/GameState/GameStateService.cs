using Stgadgets.Core.Derived;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;

namespace Stgadgets.Server.GameState;

/// <summary>
/// The main loop: find what's running, pick its pack, then decode, derive and publish every snapshot the provider
/// pushes (docs/architecture.md#the-loop). When the packs are reloaded, it starts again from finding what's running.
/// </summary>
public sealed class GameStateService(
    IEmulatorProvider provider,
    PackLibrary packs,
    VariableBroadcaster broadcaster,
    ILogger<GameStateService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogPacks("Loaded");

        string? lastWarning = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            var reloaded = packs.Changed;
            using var run = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, reloaded);
            try
            {
                await RunOnceAsync(packs.Catalog, run.Token);
                lastWarning = null;
            }
            // However the provider reacts to being cancelled, a reload just starts the loop again.
            catch (Exception) when (reloaded.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
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

            if (reloaded.IsCancellationRequested)
            {
                LogPacks("Pack files changed; reloaded");
                continue;
            }
            await Task.Delay(RetryDelay, stoppingToken);
        }
    }

    private void LogPacks(string what) =>
        logger.LogInformation("{What} {Count} pack(s): {Ids}", what, packs.Catalog.Packs.Count, string.Join(", ", packs.Catalog.Packs.Select(p => p.Id)));

    private async Task RunOnceAsync(PackCatalog packs, CancellationToken cancellationToken)
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
            broadcaster.SetStatus("no-pack", content.ChipCrcs is null
                ? $"No pack for '{content.Romset}'"
                : $"No pack for the program ROMs in '{content.Romset}'");
            return;
        }

        var decoder = new MapDecoder(pack.Map);
        logger.LogInformation("Playing {Game} (pack {Pack}, {Romset} on {System}, chip CRCs {ChipCrcs}); reading {Ranges}",
            pack.Game.Title, pack.Id, content.Romset, content.System, content.ChipCrcs is null ? "not found" : "read",
            string.Join(", ", decoder.Plan.Ranges));
        broadcaster.SetGame(pack, content.Romset);
        broadcaster.SetStatus("connected");

        using var derived = pack.Derived?.CreateEvaluator(pack.Map);
        var failing = new HashSet<string>();
        var settings = pack.ProviderSettings.GetValueOrDefault(provider.Id);
        await foreach (var snapshot in provider.StreamAsync(content, decoder.Plan, settings, cancellationToken))
        {
            var readAt = VariableBroadcaster.UnixMs();
            var values = decoder.Decode(snapshot);
            if (derived is not null)
                LogDerivedErrors(derived.Evaluate(values), failing);
            broadcaster.PublishTick(values, readAt);
        }

        if (!cancellationToken.IsCancellationRequested)
            logger.LogInformation("Content changed or emulator went away");
    }

    /// <summary>Logs when a derived value starts failing, and when it recovers, rather than on every tick.</summary>
    private void LogDerivedErrors(IReadOnlyList<DerivedError> errors, HashSet<string> failing)
    {
        foreach (var error in errors)
        {
            if (failing.Add(error.Name))
                logger.LogWarning("Derived value '{Name}' failed (null until it recovers): {Message}", error.Name, error.Message);
        }
        if (failing.Count > errors.Count)
        {
            foreach (var name in failing.Where(name => errors.All(e => e.Name != name)).ToList())
            {
                failing.Remove(name);
                logger.LogInformation("Derived value '{Name}' recovered", name);
            }
        }
    }
}
