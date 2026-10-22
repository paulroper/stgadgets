using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Providers;

/// <summary>What the emulator is running.</summary>
/// <param name="Romset">MAME/FBNeo short name, e.g. "invaders".</param>
/// <param name="System">The emulator's name for the system or core, e.g. "fb_alpha".</param>
/// <param name="ChipCrcs">
/// Arcade games: the CRC32 of each ROM chip, by file name (<see cref="RomSetZip"/>). Null when the provider can't find
/// the ROM set's files, and then packs match by <paramref name="Romset"/> alone.
/// </param>
public sealed record ContentInfo(string? Romset, string? System, IReadOnlyDictionary<string, uint>? ChipCrcs = null);

/// <summary>The emulator's process, so the overlay host can find its window.</summary>
/// <param name="AppName">The app's name as the OS shows it, e.g. "RetroArch".</param>
/// <param name="ProcessId">The process ID when known; the most reliable way to find the window.</param>
public sealed record EmulatorProcessInfo(string AppName, int? ProcessId);

/// <summary>
/// A pack's settings for one provider (packs/&lt;id&gt;/providers/&lt;provider&gt;.yaml), such as per-core address
/// translation. Only the provider knows their shape.
/// </summary>
public sealed record ProviderSettings(string Path, string Yaml)
{
    /// <summary>Deserializes the settings with the usual pack conventions (<see cref="PackYaml"/>).</summary>
    public T Read<T>()
    {
        try
        {
            return PackYaml.Deserialize<T>(Yaml);
        }
        catch (Exception ex) when (ex is FormatException or YamlDotNet.Core.YamlException)
        {
            throw new FormatException($"{Path}: {ex.Message}", ex);
        }
    }
}

/// <summary>Connects stgadgets to one emulator. See docs/providers.md.</summary>
public interface IEmulatorProvider : IAsyncDisposable
{
    string Id { get; }

    /// <summary>The emulator's process, once the provider has found it; null while not connected.</summary>
    EmulatorProcessInfo? Process { get; }

    /// <summary>What's running, or null when no game is loaded.</summary>
    Task<ContentInfo?> GetContentAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Streams snapshots of <paramref name="plan"/>, using the original hardware's addresses, as close to every frame as the
    /// emulator allows. Ends when the content changes or the emulator goes away.
    /// </summary>
    /// <param name="settings">The matched pack's settings for this provider, if it has any.</param>
    IAsyncEnumerable<Snapshot> StreamAsync(
        ContentInfo content, ReadPlan plan, ProviderSettings? settings, CancellationToken cancellationToken);
}
