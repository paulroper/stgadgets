namespace Stgadgets.Host.Lib.Overlay;

/// <summary>
/// How an overlay host was started, from its environment. Every host reads the same variables, because
/// <c>stgadgets dev</c> (src/cli) starts them all the same way.
/// </summary>
/// <param name="Server">The stgadgets server: STGADGETS_SERVER, by default http://127.0.0.1:5199/.</param>
/// <param name="SnapshotPort">STGADGETS_SNAPSHOT_PORT: serve snapshots on this port (<see cref="SnapshotServer"/>); off when unset.</param>
/// <param name="Experiments">STGADGETS_EXPERIMENT, comma-separated: spike experiments (docs/latency.md). Each host
/// defines its own, apart from <see cref="AlwaysShow"/>.</param>
public sealed record OverlaySettings(Uri Server, int? SnapshotPort, IReadOnlySet<string> Experiments)
{
    public static readonly Uri DefaultServer = new("http://127.0.0.1:5199/");

    /// <summary>The layout page, in its overlay mode: no page background.</summary>
    public Uri LayoutUrl => new(Server, "?overlay");

    /// <summary>Show the overlay even when the emulator isn't the active app, or has no window.</summary>
    public bool AlwaysShow => Experiment("always-show");

    public bool Experiment(string name) => Experiments.Contains(name);

    public static OverlaySettings FromEnvironment() => From(Environment.GetEnvironmentVariable);

    public static OverlaySettings From(Func<string, string?> variable) => new(
        variable("STGADGETS_SERVER") is { Length: > 0 } server ? new Uri(server) : DefaultServer,
        int.TryParse(variable("STGADGETS_SNAPSHOT_PORT"), out var port) ? port : null,
        (variable("STGADGETS_EXPERIMENT") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet());
}
