using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Recordings;

/// <summary><c>stgadgets recording values</c>: every value the server would publish at points in a recording.</summary>
internal static class ValuesCommand
{
    public static Command Create()
    {
        var recording = RecordingCommand.Argument();
        var at = new Option<string[]>("--at")
        {
            Description = $"A point in the recording: {RecordingCommand.PointHelp}. Repeat for several.",
            Required = true,
        };
        var only = new Option<string[]>("--only")
        {
            Description = "Only the values whose names match; * matches anything, e.g. 'p1.*'. Repeat for several.",
        };
        var command = new Command(
            "values",
            "Every value the server would publish at points in a recording, map and derived.js alike, as JSON. Use it to " +
            "start a pack's game states (tests/states.ts) or scenarios (tests/derived) from real play.")
        {
            recording, at, only,
        };
        command.SetAction(StgadgetsCommand.Handle(result =>
            Write(result.GetRequiredValue(recording), result.GetRequiredValue(at), result.GetValue(only) ?? [], result.InvocationConfiguration)));
        return command;
    }

    private static void Write(DirectoryInfo directory, string[] points, string[] only, InvocationConfiguration console)
    {
        var recording = RecordingCommand.Load(directory);
        var pack = RecordingCommand.RequirePackOf(directory);
        var ticks = points.Distinct().ToDictionary(point => point, point => RecordingCommand.Tick(recording, point));

        var patterns = only.Select(pattern => new Regex($"^{Regex.Escape(pattern).Replace(@"\*", ".*")}$")).ToList();
        var wanted = new Dictionary<int, SortedDictionary<string, object?>>();
        var failed = new HashSet<string>();
        foreach (var (tick, values, errors) in RecordingReplay.Run(recording, new MapDecoder(pack.Map), pack.Derived).Take(ticks.Values.Max() + 1))
        {
            foreach (var error in errors.Where(e => failed.Add(e.Name)))
                console.Error.WriteLine($"tick {tick}: derived '{error.Name}' failed (null from then on until it recovers): {error.Message}");
            if (ticks.ContainsValue(tick))
                wanted[tick] = new(values.Where(v => patterns.Count == 0 || patterns.Any(p => p.IsMatch(v.Key))).ToDictionary());
        }

        var json = ticks.ToDictionary(point => point.Key, point => new { tick = point.Value, values = wanted[point.Value] });
        console.Output.WriteLine(JsonSerializer.Serialize(json, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // readable quotes and apostrophes in marker labels and strings
    };
}
