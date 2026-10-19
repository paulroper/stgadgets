using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Recordings;

/// <summary>
/// Changes to recordings on disk. The manifest is edited as JSON, so fields that <see cref="RecordingManifest"/>
/// doesn't model (such as when it was captured) are kept.
/// </summary>
public static class RecordingEdits
{
    private const int TickEntrySize = 16;   // int64 microseconds + int64 emulated frame, as in Recording

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Adds a marker at <paramref name="tick"/>, or moves the marker with this label there.</summary>
    public static void Mark(string directory, int tick, string label)
    {
        var path = Path.Combine(directory, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var ticks = manifest["ticks"]!.GetValue<int>();
        if (tick < 0 || tick >= ticks)
            throw new ArgumentOutOfRangeException(nameof(tick), $"Tick {tick} isn't in the recording (0 to {ticks - 1})");

        var markers = (manifest["markers"]?.AsArray() ?? [])
            .Select(m => m!.DeepClone())
            .Where(m => m["label"]!.GetValue<string>() != label)
            .Append(new JsonObject { ["tick"] = tick, ["label"] = label })
            .OrderBy(m => m["tick"]!.GetValue<int>());
        manifest["markers"] = new JsonArray([.. markers]);
        File.WriteAllText(path, manifest.ToJsonString(Indented));
    }

    /// <summary>
    /// Copies ticks <paramref name="first"/> to <paramref name="last"/> to a new recording in <paramref name="outDirectory"/>,
    /// with the markers in that range and timestamps starting again from 0.
    /// </summary>
    public static void Trim(string directory, int first, int last, string outDirectory)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")))!.AsObject();
        var ticks = manifest["ticks"]!.GetValue<int>();
        if (first < 0 || first > last || last >= ticks)
            throw new ArgumentOutOfRangeException(nameof(first), $"Ticks {first} to {last} aren't a range in the recording (0 to {ticks - 1})");
        if (Directory.Exists(outDirectory))
            throw new IOException($"{outDirectory} already exists");
        Directory.CreateDirectory(outDirectory);

        foreach (var region in manifest["regions"]!.AsArray())
        {
            var size = (int)PackYaml.ParseNumber(region!["size"]!.GetValue<string>());
            var file = region["file"]!.GetValue<string>();
            var data = File.ReadAllBytes(Path.Combine(directory, file));
            File.WriteAllBytes(Path.Combine(outDirectory, file), data[(first * size)..((last + 1) * size)]);
        }

        var entries = File.ReadAllBytes(Path.Combine(directory, "ticks.bin"))[(first * TickEntrySize)..((last + 1) * TickEntrySize)];
        var start = BinaryPrimitives.ReadInt64LittleEndian(entries);
        for (var i = 0; i < entries.Length; i += TickEntrySize)
        {
            var micros = entries.AsSpan(i, 8);
            BinaryPrimitives.WriteInt64LittleEndian(micros, BinaryPrimitives.ReadInt64LittleEndian(micros) - start);
        }
        File.WriteAllBytes(Path.Combine(outDirectory, "ticks.bin"), entries);

        manifest["ticks"] = last - first + 1;
        manifest["markers"] = new JsonArray([.. (manifest["markers"]?.AsArray() ?? [])
            .Where(m => m!["tick"]!.GetValue<int>() is var tick && tick >= first && tick <= last)
            .Select(m => new JsonObject { ["tick"] = m!["tick"]!.GetValue<int>() - first, ["label"] = m["label"]!.GetValue<string>() })]);
        var notes = manifest["notes"]?.GetValue<string>();
        manifest["notes"] = $"{notes} Trimmed to ticks {first}..{last} of {Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))}.".Trim();
        File.WriteAllText(Path.Combine(outDirectory, "manifest.json"), manifest.ToJsonString(Indented));
    }
}
