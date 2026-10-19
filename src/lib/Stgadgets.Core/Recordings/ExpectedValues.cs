using System.Globalization;
using Stgadgets.Core.Derived;
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
                IReadOnlyDictionary<string, object?> (point) => point.Value.ToDictionary(v => v.Key, v => PackYaml.ParseValue(v.Value))),
            dto.Sequences.ToDictionary(
                sequence => sequence.Key,
                IReadOnlyList<object?> (sequence) => sequence.Value.Select(PackYaml.ParseValue).ToList()));
    }

    /// <summary>
    /// Replays <paramref name="recording"/> through <paramref name="decoder"/>, and <paramref name="derived"/> if given,
    /// and describes every mismatch and every derived value that failed.
    /// </summary>
    public IReadOnlyList<string> Check(Recording recording, MapDecoder decoder, DerivedScript? derived = null)
    {
        var failures = new List<string>();
        var failed = new HashSet<string>();
        var ticks = new List<Dictionary<string, object?>>();
        foreach (var (tick, values, errors) in RecordingReplay.Run(recording, decoder, derived))
        {
            ticks.Add(values);
            foreach (var error in errors)
            {
                if (failed.Add(error.Name))   // report each value's first failure only
                    failures.Add($"tick {tick}: derived '{error.Name}' failed: {error.Message}");
            }
        }

        foreach (var (point, values) in At)
        {
            if (recording.FindTick(point) is not { } tick)
            {
                failures.Add($"at '{point}': no such tick or marker");
                continue;
            }
            foreach (var (name, expected) in values)
            {
                if (!ticks[tick].TryGetValue(name, out var actual))
                    failures.Add($"at '{point}': the map has no variable '{name}'");
                else if (!Equals(expected, actual))
                    failures.Add($"at '{point}' (tick {tick}): {name} is {PackYaml.FormatValue(actual)}, expected {PackYaml.FormatValue(expected)}");
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
                failures.Add($"sequences: {name} is [{string.Join(", ", actual.Select(PackYaml.FormatValue))}], expected [{string.Join(", ", expected.Select(PackYaml.FormatValue))}]");
        }

        return failures;
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

    // YAML shape. Values are read as strings so hex like 0xFF parses the same everywhere.
    private sealed class ExpectedDto
    {
        public Dictionary<string, Dictionary<string, string?>> At { get; set; } = [];
        public Dictionary<string, List<string?>> Sequences { get; set; } = [];
    }
}
