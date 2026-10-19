using System.CommandLine;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Recordings;

/// <summary>
/// <c>stgadgets recording</c>: find addresses in, inspect, mark and trim a pack's local recordings
/// (docs/cli.md#recordings).
/// </summary>
internal static class RecordingCommand
{
    public static Command Create() => new("recording", "Find addresses in, inspect, mark and trim recordings.")
    {
        MarkersCommand.Create(),
        SearchCommand.Create(),
        HistoryCommand.Create(),
        ValuesCommand.Create(),
        MarkCommand.Create(),
        TrimCommand.Create(),
    };

    public static Argument<DirectoryInfo> Argument() => new("recording")
    {
        Description = "The recording's folder, e.g. packs/ddonpach/recordings/play-01",
    };

    public const string PointHelp = "a tick number, 'start', 'end', or a marker label (exact, or a unique prefix)";

    public static Recording Load(DirectoryInfo directory) =>
        File.Exists(Path.Combine(directory.FullName, "manifest.json"))
            ? Recording.Load(directory.FullName)
            : throw new UsageException($"{directory.FullName} isn't a recording: it has no manifest.json");

    /// <summary>The pack a recording is in (packs/&lt;id&gt;/recordings/&lt;name&gt;), or null if it isn't in one.</summary>
    public static Pack? PackOf(DirectoryInfo recording)
    {
        var pack = recording.Parent?.Parent;
        return pack is not null && File.Exists(Path.Combine(pack.FullName, "pack.yaml")) ? PackLoader.Load(pack.FullName) : null;
    }

    public static Pack RequirePackOf(DirectoryInfo recording) =>
        PackOf(recording) ?? throw new UsageException($"{recording.FullName} isn't in a pack: expected packs/<id>/recordings/<name>");

    /// <summary>The tick a point names (see <see cref="PointHelp"/>).</summary>
    public static int Tick(Recording recording, string point)
    {
        if (point == "end")
            return recording.Count - 1;
        if (recording.FindTick(point) is { } tick)
            return tick;
        if (int.TryParse(point, out _))
            throw new UsageException($"Tick {point} isn't in the recording (0 to {recording.Count - 1})");

        var markers = recording.Manifest.Markers ?? [];
        var matches = markers.Where(m => m.Label.StartsWith(point, StringComparison.Ordinal)).ToList();
        return matches.Count == 1
            ? matches[0].Tick
            : throw new UsageException(
                $"{(matches.Count == 0 ? "No tick or marker" : "Several markers match")} '{point}'. Markers: {string.Join(", ", markers.Select(m => $"'{m.Label}'"))}");
    }

    /// <summary>Labels of the markers at <paramref name="tick"/>.</summary>
    public static IEnumerable<string> MarkersAt(Recording recording, int tick) =>
        (recording.Manifest.Markers ?? []).Where(m => m.Tick == tick).Select(m => m.Label);
}
