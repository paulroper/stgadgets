using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Recordings;

public sealed record RecordingContent(string? Romset, string? Core, string? Provider);

public sealed record RecordingRegion(string Name, string Start, string Size, string File);

public sealed record RecordingMarker(int Tick, string Label);

public sealed record RecordingManifest(
    int Schema,
    RecordingContent Content,
    bool FrameSynced,
    int Ticks,
    IReadOnlyList<RecordingRegion> Regions,
    IReadOnlyList<RecordingMarker>? Markers,
    string? Notes,
    DateTimeOffset? CapturedAt = null);

/// <summary>A captured run of memory snapshots. Format: docs/providers.md#recording-format-draft.</summary>
public sealed class Recording
{
    private const int TickEntrySize = 16;   // int64 microseconds + int64 emulated frame (-1 if unknown)

    private readonly (AddressRange Range, byte[] Data)[] _regions;
    private readonly byte[] _ticks;

    private Recording(RecordingManifest manifest, (AddressRange, byte[])[] regions, byte[] ticks)
    {
        Manifest = manifest;
        _regions = regions;
        _ticks = ticks;
    }

    public RecordingManifest Manifest { get; }

    public int Count => Manifest.Ticks;

    public static Recording Load(string directory)
    {
        var manifest = JsonSerializer.Deserialize<RecordingManifest>(
            File.ReadAllText(Path.Combine(directory, "manifest.json")), JsonSerializerOptions.Web)
            ?? throw new FormatException("Empty recording manifest");

        var regions = manifest.Regions.Select(r =>
        {
            var range = new AddressRange(PackYaml.ParseNumber(r.Start), (int)PackYaml.ParseNumber(r.Size));
            var data = File.ReadAllBytes(Path.Combine(directory, r.File));
            if (data.Length != range.Length * manifest.Ticks)
                throw new FormatException($"{r.File}: expected {range.Length * manifest.Ticks} bytes, found {data.Length}");
            return (range, data);
        }).ToArray();

        var ticks = File.ReadAllBytes(Path.Combine(directory, "ticks.bin"));
        if (ticks.Length != TickEntrySize * manifest.Ticks)
            throw new FormatException($"ticks.bin: expected {TickEntrySize * manifest.Ticks} bytes, found {ticks.Length}");

        return new Recording(manifest, regions, ticks);
    }

    public Snapshot this[int tick]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tick);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tick, Count);

            var entry = _ticks.AsSpan(tick * TickEntrySize, TickEntrySize);
            var micros = BinaryPrimitives.ReadInt64LittleEndian(entry);
            var frame = BinaryPrimitives.ReadInt64LittleEndian(entry[8..]);
            var blocks = _regions
                .Select(r => new MemoryBlock(r.Range, r.Data.AsMemory(tick * r.Range.Length, r.Range.Length)))
                .ToList();
            return new Snapshot(frame < 0 ? null : frame, TimeSpan.FromMicroseconds(micros), blocks);
        }
    }

    public IEnumerable<Snapshot> Snapshots => Enumerable.Range(0, Count).Select(i => this[i]);

    /// <summary>The tick a point names: a tick number, a marker label, or "start" (tick 0); null if there's no such tick.</summary>
    public int? FindTick(string point)
    {
        if (point == "start")
            return 0;
        if (int.TryParse(point, CultureInfo.InvariantCulture, out var tick))
            return tick >= 0 && tick < Count ? tick : null;
        return Manifest.Markers?.FirstOrDefault(m => m.Label == point)?.Tick;
    }
}
