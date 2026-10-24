using System.Globalization;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Derived;

/// <summary>
/// A file of scenarios for a pack's derived.js (packs/&lt;id&gt;/tests/derived/*.yaml): made-up ticks of map variables,
/// and the derived values they must produce. See "Testing derived values" in docs/packs.md. Unlike recordings, they're
/// committed, and don't depend on the script engine, so they also check a port of derived.js.
/// </summary>
/// <param name="Start">Map variables on every scenario's first tick. Variables not set anywhere are zero.</param>
/// <param name="Add">Amounts added to map variables on every tick of every scenario, e.g. a frame counter's 1.</param>
/// <param name="Scenarios">Each scenario's steps, by name.</param>
public sealed record DerivedScenarios(
    IReadOnlyDictionary<string, object?> Start,
    IReadOnlyDictionary<string, long> Add,
    IReadOnlyDictionary<string, IReadOnlyList<ScenarioStep>> Scenarios)
{
    /// <summary>Loads a scenario file.</summary>
    public static DerivedScenarios Load(string path) => PackYaml.LoadFile(path, Parse);

    public static DerivedScenarios Parse(string yaml)
    {
        var dto = PackYaml.Deserialize<FileDto>(yaml);
        return new DerivedScenarios(
            ParseValues(dto.Start),
            ParseAmounts(dto.Add),
            dto.Scenarios.ToDictionary(
                scenario => scenario.Key,
                IReadOnlyList<ScenarioStep> (scenario) => scenario.Value.Select(step => new ScenarioStep(
                    ParseValues(step.Set),
                    ParseAmounts(step.Add),
                    step.Repeat >= 1 ? step.Repeat : throw new FormatException($"'{scenario.Key}': repeat must be at least 1"),
                    step.Expect.ToDictionary(e => e.Key, e => ExpectedValue.Parse(e.Value)))).ToList()));
    }

    /// <summary>
    /// Runs each scenario through <paramref name="derived"/>, from a fresh start, and describes every mismatch and
    /// every derived value that failed.
    /// </summary>
    public IReadOnlyList<string> Check(MemoryMap map, DerivedScript derived)
    {
        var failures = new List<string>();
        var mapVariables = new MapDecoder(map).DecodeFilled(0);
        var derivedNames = derived.Values.Select(v => v.Name).ToHashSet();
        bool IsSettable(string path) => ValuePath.Parse(path).TryGet(mapVariables, out _);

        failures.AddRange(Start.Keys.Where(name => !IsSettable(name)).Select(name => $"start: {NotSettable(name, derivedNames)}"));
        failures.AddRange(Add.Keys.Where(name => !IsSettable(name)).Select(name => $"add: {NotSettable(name, derivedNames)}"));

        using var evaluator = derived.CreateEvaluator(map, DerivedScript.ReplayTickLimit);
        foreach (var (name, steps) in Scenarios)
        {
            if (steps.All(step => step.Expect.Count == 0))
                failures.Add($"'{name}': expects nothing");

            evaluator.Restart();
            var memory = new Dictionary<string, object?>(mapVariables);
            foreach (var (variable, value) in Start)
                ValuePath.Parse(variable).TrySet(memory, value);

            var tick = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var where = $"'{name}' step {i + 1}";
                foreach (var variable in step.Set.Keys.Concat(step.Add.Keys).Where(v => !IsSettable(v)))
                    failures.Add($"{where}: {NotSettable(variable, derivedNames)}");
                Dictionary<string, object?> values = [];
                for (var repeat = 0; repeat < step.Repeat; repeat++, tick++)
                {
                    // Adds first, so the step's first tick has exactly the values it sets.
                    if (tick > 0)
                    {
                        foreach (var (variable, amount) in Add.Concat(step.Add).Where(v => IsSettable(v.Key)))
                        {
                            var path = ValuePath.Parse(variable);
                            path.TryGet(memory, out var current);
                            path.TrySet(memory, current is long number ? number + amount : null);
                        }
                    }
                    if (repeat == 0)
                    {
                        foreach (var (variable, value) in step.Set)
                            ValuePath.Parse(variable).TrySet(memory, value);
                    }
                    values = new Dictionary<string, object?>(memory);
                    foreach (var error in evaluator.Evaluate(values))
                        failures.Add($"{where} (tick {tick}): derived '{error.Name}' failed: {error.Message}");
                }

                foreach (var (variable, expected) in step.Expect)
                {
                    if (!ValuePath.Parse(variable).TryGet(values, out var actual))
                        failures.Add($"{where}: no map variable or derived value '{variable}'");
                    else if (!expected.Matches(actual))
                        failures.Add($"{where} (tick {tick - 1}): {variable} is {PackYaml.FormatValue(actual)}, expected {expected}");
                }
            }
        }

        return failures;
    }

    private static string NotSettable(string name, HashSet<string> derivedNames) => derivedNames.Contains(name)
        ? $"'{name}' is a derived value; set the map variables it comes from"
        : $"the map has no variable '{name}'";

    private static Dictionary<string, object?> ParseValues(Dictionary<string, string?> values) =>
        values.ToDictionary(v => v.Key, v => PackYaml.ParseValue(v.Value));

    private static Dictionary<string, long> ParseAmounts(Dictionary<string, string?> amounts) =>
        amounts.ToDictionary(a => a.Key, a => PackYaml.ParseValue(a.Value) as long? ?? throw new FormatException($"add: '{a.Key}' must be a whole number"));

    // YAML shape. Values are read as strings so hex like 0xFF parses the same everywhere.
    private sealed class FileDto
    {
        public Dictionary<string, string?> Start { get; set; } = [];
        public Dictionary<string, string?> Add { get; set; } = [];
        public Dictionary<string, List<StepDto>> Scenarios { get; set; } = [];
    }

    private sealed class StepDto
    {
        public Dictionary<string, string?> Set { get; set; } = [];
        public Dictionary<string, string?> Add { get; set; } = [];
        public int Repeat { get; set; } = 1;
        public Dictionary<string, string?> Expect { get; set; } = [];
    }
}

/// <summary>One step of a scenario: change some map variables, run <paramref name="Repeat"/> ticks, then check values.</summary>
/// <param name="Set">Map variables to change; the others keep the previous step's values.</param>
/// <param name="Add">Amounts added to map variables on each of the step's ticks, as well as the file's. The step's first
/// tick still has exactly the values in <paramref name="Set"/>.</param>
/// <param name="Repeat">How many ticks the step lasts. Its expectations are checked on the last.</param>
/// <param name="Expect">Values, map or derived, after the step's last tick.</param>
public sealed record ScenarioStep(
    IReadOnlyDictionary<string, object?> Set,
    IReadOnlyDictionary<string, long> Add,
    int Repeat,
    IReadOnlyDictionary<string, ExpectedValue> Expect);

/// <summary>
/// An expected value. A fraction matches to the decimal places it's written with: 73.4 matches 73.396.
/// </summary>
public sealed record ExpectedValue(object? Value, int? Decimals)
{
    public static ExpectedValue Parse(string? text)
    {
        var value = PackYaml.ParseValue(text);
        var point = text?.IndexOf('.') ?? -1;
        return new ExpectedValue(value, value is double && point >= 0 && !text!.Contains('e', StringComparison.OrdinalIgnoreCase) ? text.Length - point - 1 : null);
    }

    public bool Matches(object? actual) => Value is double expected && Decimals is { } decimals && actual is double or long
        ? Math.Round(Convert.ToDouble(actual, CultureInfo.InvariantCulture), decimals) == expected
        : Equals(Value, actual);

    public override string ToString() => PackYaml.FormatValue(Value);
}
