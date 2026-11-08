using System.CommandLine;
using Stgadgets.Cli.Dev;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;
using Stgadgets.Providers;

namespace Stgadgets.Cli.Peek;

/// <summary>
/// <c>stgadgets peek memory</c>: the map variables of the game running in an emulator, read once, through the pack
/// that matches it. For checking a variable against the screen without making a recording.
/// </summary>
internal static class MemoryCommand
{
    /// <param name="providers">The providers to choose from, by ID; tests add a fake.</param>
    public static Command Create(IReadOnlyDictionary<string, ConnectProvider>? providers = null)
    {
        // Peek is for live play; recordings are read with `stgadgets recording`.
        providers ??= EmulatorProviders.All.Where(p => p.Key != "replay").ToDictionary();

        var names = PeekCommand.Names("Map variables");
        var provider = new Option<string>("--provider")
        {
            Description = $"The emulator to read from: {string.Join(", ", providers.Keys)}.",
            DefaultValueFactory = _ => "retroarch",
        };
        provider.AcceptOnlyFromAmong([.. providers.Keys]);
        var endpoint = new Option<string>("--endpoint")
        {
            Description = "Where the emulator listens, in the provider's form (retroarch: host:port, default 127.0.0.1:55355).",
        };
        var packs = new Option<DirectoryInfo>("--packs")
        {
            Description = "The folder of packs to find the game's in. Default: the repository's packs/.",
        };

        var command = new Command("memory", "Read map variables once from the game running in an emulator, through its pack.")
        {
            names, provider, endpoint, packs,
        };
        command.SetAction(StgadgetsCommand.HandleAsync(async (result, cancellationToken) =>
        {
            var packsDirectory = result.GetValue(packs)?.FullName
                ?? Path.Combine(DevStack.FindRepoRoot(Directory.GetCurrentDirectory()), "packs");
            var providerId = result.GetRequiredValue(provider);

            await using var emulator = providers[providerId](result.GetValue(endpoint), Directory.GetCurrentDirectory());
            var content = await emulator.GetContentAsync(cancellationToken) ?? throw new UsageException($"{providerId}: no game loaded");
            var pack = PackCatalog.LoadDirectory(packsDirectory).Find(content)
                ?? throw new UsageException($"No pack in {packsDirectory} matches '{content.Romset}'");
            var decoder = new MapDecoder(pack.Map);
            var snapshot = await FirstSnapshotAsync(emulator, content, decoder.Plan, pack.ProviderSettings.GetValueOrDefault(providerId), cancellationToken);

            PeekCommand.Write(decoder.Decode(snapshot), result.GetValue(names) ?? [], $"{pack.Id}'s map", result.InvocationConfiguration.Output);
        }));
        return command;
    }

    private static async Task<Snapshot> FirstSnapshotAsync(
        IEmulatorProvider emulator, ContentInfo content, ReadPlan plan, ProviderSettings? settings, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in emulator.StreamAsync(content, plan, settings, cancellationToken))
                return snapshot;
        }
        catch (NotSupportedException ex)   // the provider can't read this game as set up, e.g. no core profile
        {
            throw new UsageException($"{emulator.Id}: {ex.Message}. Add the settings to the pack's providers/{emulator.Id}.yaml (docs/providers.md)");
        }
        throw new UsageException($"{emulator.Id} sent nothing: the game stopped or changed");
    }
}
