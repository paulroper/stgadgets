using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Providers;

/// <summary>What the emulator is running.</summary>
/// <param name="Romset">MAME/FBNeo short name, e.g. "invaders".</param>
/// <param name="System">The emulator's name for the system or core, e.g. "fb_alpha".</param>
public sealed record ContentInfo(string? Romset, string? System);

/// <summary>The emulator's process, so the overlay host can find its window.</summary>
/// <param name="AppName">The app's name as the OS shows it, e.g. "RetroArch".</param>
/// <param name="ProcessId">The process ID when known; the most reliable way to find the window.</param>
public sealed record EmulatorProcessInfo(string AppName, int? ProcessId);

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
    IAsyncEnumerable<Snapshot> StreamAsync(ContentInfo content, ReadPlan plan, CancellationToken cancellationToken);
}
