using System.Buffers.Binary;
using System.Text.Json;
using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Recordings;

/// <summary>
/// Writes snapshots to a new recording as they arrive (docs/providers.md#recording-format-draft): one file of bytes per
/// region and ticks.bin as it goes, and the manifest when it's disposed, so a recording that stops early is still
/// usable.
/// </summary>
public sealed class RecordingWriter : IDisposable
{
    private const int TickEntrySize = 16;   // int64 microseconds + int64 emulated frame (-1 if unknown), as in Recording

    private readonly string _directory;
    private readonly IReadOnlyList<(string Name, AddressRange Range, FileStream File)> _regions;
    private readonly FileStream _ticks;
    private readonly RecordingContent _content;
    private readonly DateTimeOffset _capturedAt;
    private readonly List<RecordingMarker> _markers = [];
    private bool _frameSynced = true;

    /// <summary>Starts a recording in <paramref name="directory"/>, which mustn't exist yet.</summary>
    public RecordingWriter(string directory, IReadOnlyList<(string Name, AddressRange Range)> regions, RecordingContent content, DateTimeOffset capturedAt)
    {
        if (Directory.Exists(directory))
            throw new IOException($"{directory} already exists");
        if (regions.Select(r => r.Name).Distinct().Count() != regions.Count)
            throw new ArgumentException("Region names must be unique", nameof(regions));
        Directory.CreateDirectory(directory);

        _directory = directory;
        _regions = regions.Select(r => (r.Name, r.Range, File.Create(Path.Combine(directory, $"{r.Name}.bin")))).ToList();
        _ticks = File.Create(Path.Combine(directory, "ticks.bin"));
        _content = content;
        _capturedAt = capturedAt;
    }

    /// <summary>Ticks written so far.</summary>
    public int Count { get; private set; }

    /// <summary>Notes for the manifest: how it was recorded, and what happens in it.</summary>
    public string? Notes { get; set; }

    /// <summary>Appends a snapshot, which must cover every region.</summary>
    public void Write(Snapshot snapshot)
    {
        foreach (var (_, range, file) in _regions)
            file.Write(snapshot.Read(range.Start, range.Length));

        Span<byte> entry = stackalloc byte[TickEntrySize];
        BinaryPrimitives.WriteInt64LittleEndian(entry, (long)snapshot.Timestamp.TotalMicroseconds);
        BinaryPrimitives.WriteInt64LittleEndian(entry[8..], snapshot.EmulatedFrame ?? -1);
        _ticks.Write(entry);
        _frameSynced &= snapshot.EmulatedFrame is not null;
        Count++;
    }

    /// <summary>Marks the latest tick.</summary>
    public void Mark(string label)
    {
        if (Count == 0)
            throw new InvalidOperationException("Nothing to mark yet");
        _markers.Add(new RecordingMarker(Count - 1, label));
    }

    /// <summary>Finishes the recording, writing its manifest.</summary>
    public void Dispose()
    {
        foreach (var (_, _, file) in _regions)
            file.Dispose();
        _ticks.Dispose();

        var manifest = new RecordingManifest(
            Schema: 1,
            _content,
            FrameSynced: _frameSynced && Count > 0,
            Count,
            _regions.Select(r => new RecordingRegion(r.Name, $"0x{r.Range.Start:X}", $"0x{r.Range.Length:X}", $"{r.Name}.bin")).ToList(),
            _markers,
            Notes,
            _capturedAt);
        File.WriteAllText(Path.Combine(_directory, "manifest.json"), JsonSerializer.Serialize(manifest, ManifestJson));
    }

    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerOptions.Web) { WriteIndented = true };
}
