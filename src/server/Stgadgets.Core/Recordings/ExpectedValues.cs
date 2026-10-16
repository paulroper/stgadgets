using System.Globalization;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Recordings;

/// <summary>
/// The values a map must decode from a recording: the recording's expected.yaml. See "Testing" in docs/packs.md.
/// </summary>
/// <param name="At">Values at a point in the recording: a tick number, a marker label, or "start" (tick 0).</param>
/// <param name="Sequences">Every distinct value a variable takes, in order, over the whole recording.</param>
public sealed record ExpectedValues(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> At,
    IReadOnlyDictionary<string, IReadOnlyList<object?>> Sequences)
{
    public const string FileName = "expected.yaml";

    /// <summary>No expectations: <see cref="Check"/> still decodes every tick, so the map must fit the recording.</summary>
    public static ExpectedValues None { get; } = new(new Dictionary<string, IReadOnlyDictionary<string, object?>>(), new Dictionary<string, IReadOnlyList<object?>>());

    /// <summary>Loads <paramref name="recordingDirectory"/>/expected.yaml, or <see cref="None"/> if there isn't one.</summary>
    public static ExpectedValues Load(string recordingDirectory)
    {
        var path = Path.Combine(recordingDirectory, FileName);
        return File.Exists(path) ? PackYaml.LoadFile(path, Parse) : None;
    }

    public static ExpectedValues Parse(string yaml)
    {
        var dto = PackYaml.Deserialize<ExpectedDto>(yaml);
        return new ExpectedValues(
            dto.At.ToDictionary(
                point => point.Key,
                IReadOnlyDictionary<string, object?> (point) => point.Value.ToDictionary(v => v.Key, v => ParseValue(v.Value))),
            dto.Sequences.ToDictionary(
                sequence => sequence.Key,
                IReadOnlyList<object?> (sequence) => sequence.Value.Select(ParseValue).ToList()));
    }

    /// <summary>Replays <paramref name="recording"/> through <paramref name="decoder"/> and describes every mismatch.</summary>
    public IReadOnlyList<string> Check(Recording recording, MapDecoder decoder)
    {
        var ticks = recording.Snapshots.Select(decoder.Decode).ToList();
        var failures = new List<string>();

        foreach (var (point, values) in At)
        {
            if (FindTick(point, recording) is not { } tick)
            {
                failures.Add($"at '{point}': no such tick or marker");
                continue;
            }
            foreach (var (name, expected) in values)
            {
                if (!ticks[tick].TryGetValue(name, out var actual))
                    failures.Add($"at '{point}': the map has no variable '{name}'");
                else if (!Equals(expected, actual))
                    failures.Add($"at '{point}' (tick {tick}): {name} is {Format(actual)}, expected {Format(expected)}");
            }
        }

        foreach (var (name, expected) in Sequences)
        {
            if (ticks.Count > 0 && !ticks[0].ContainsKey(name))
            {
                failures.Add($"sequences: the map has no variable '{name}'");
                continue;
            }
            var actual = Distinct(ticks.Select(t => t[name]));
            if (!actual.SequenceEqual(expected))
                failures.Add($"sequences: {name} is [{string.Join(", ", actual.Select(Format))}], expected [{string.Join(", ", expected.Select(Format))}]");
        }

        return failures;
    }

    private static int? FindTick(string point, Recording recording)
    {
        if (point == "start")
            return 0;
        if (int.TryParse(point, CultureInfo.InvariantCulture, out var tick))
            return tick >= 0 && tick < recording.Count ? tick : null;
        return recording.Manifest.Markers?.FirstOrDefault(m => m.Label == point)?.Tick;
    }

    /// <summary>Consecutive duplicates removed.</summary>
    private static List<object?> Distinct(IEnumerable<object?> values)
    {
        var distinct = new List<object?>();
        foreach (var value in values)
        {
            if (distinct.Count == 0 || !Equals(distinct[^1], value))
                distinct.Add(value);
        }
        return distinct;
    }

    /// <summary>Values as the decoder produces them: <see cref="long"/>, <see cref="bool"/> or null.</summary>
    private static object? ParseValue(string? text) => text switch
    {
        null or "null" or "~" => null,
        "true" => true,
        "false" => false,
        _ when text.StartsWith('-') => -(long)PackYaml.ParseNumber(text[1..]),
        _ => (long)PackYaml.ParseNumber(text),
    };

    private static string Format(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };

    // YAML shape. Values are read as strings so hex like 0xFF parses the same everywhere.
    private sealed class ExpectedDto
    {
        public Dictionary<string, Dictionary<string, string?>> At { get; set; } = [];
        public Dictionary<string, List<string?>> Sequences { get; set; } = [];
    }
}
