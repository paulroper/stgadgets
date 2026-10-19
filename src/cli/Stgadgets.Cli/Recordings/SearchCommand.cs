using System.CommandLine;
using System.CommandLine.Parsing;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Recordings;

/// <summary>
/// <c>stgadgets recording search</c>: narrow down which addresses hold a value, the way cheat finders do. Each filter
/// compares one byte between two points and keeps the addresses that pass, in the order the filters are given.
/// </summary>
internal static class SearchCommand
{
    private static readonly Dictionary<string, (string Description, Func<byte, byte, bool> Passes)> Comparisons = new()
    {
        ["--changed"] = ("changed", (a, b) => a != b),
        ["--unchanged"] = ("didn't change", (a, b) => a == b),
        ["--increased"] = ("went up", (a, b) => b > a),
        ["--decreased"] = ("went down", (a, b) => b < a),
    };

    public static Command Create()
    {
        var recording = RecordingCommand.Argument();
        var region = new Option<string>("--region") { Description = "Search only this region of the recording (default: all)." };
        var show = new Option<int>("--show") { Description = "How many candidates to list.", DefaultValueFactory = _ => 20 };
        var command = new Command(
            "search",
            $"Narrow down which addresses hold a value. Each filter compares one byte between two points ({RecordingCommand.PointHelp}) " +
            "and keeps the addresses that pass; filters apply in the order given.")
        {
            recording, region, show,
        };
        foreach (var (name, (description, _)) in Comparisons)
        {
            command.Options.Add(new Option<string[]>(name)
            {
                Description = $"Keep addresses whose byte {description} between two points.",
                HelpName = "from> <to",
                Arity = new ArgumentArity(2, 2),
                AllowMultipleArgumentsPerToken = true,
            });
        }
        command.Options.Add(new Option<string[]>("--equals")
        {
            Description = "Keep addresses whose byte equals a value (decimal or 0x hex) at a point.",
            HelpName = "point> <value",
            Arity = new ArgumentArity(2, 2),
            AllowMultipleArgumentsPerToken = true,
        });

        command.SetAction(StgadgetsCommand.Handle(result =>
        {
            var loaded = RecordingCommand.Load(result.GetRequiredValue(recording));
            Search(loaded, Filters(result), result.GetValue(region), result.GetValue(show), result.InvocationConfiguration.Output);
        }));
        return command;
    }

    // The filters in command-line order. Options are collected per name, so the order comes from the tokens.
    private static List<(string Name, string[] Operands)> Filters(ParseResult result)
    {
        var filters = new List<(string, string[])>();
        var tokens = result.Tokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type != TokenType.Option || !(Comparisons.ContainsKey(tokens[i].Value) || tokens[i].Value == "--equals"))
                continue;
            var operands = tokens.Skip(i + 1).TakeWhile(t => t.Type == TokenType.Argument).Select(t => t.Value).ToArray();
            if (operands.Length != 2)
                throw new UsageException($"{tokens[i].Value} takes two values, e.g. {tokens[i].Value} start end");
            filters.Add((tokens[i].Value, operands));
        }
        return filters;
    }

    private static void Search(Recording recording, List<(string Name, string[] Operands)> filters, string? regionName, int show, TextWriter output)
    {
        var regions = recording.Manifest.Regions.Where(r => regionName is null || r.Name == regionName).ToList();
        if (regions.Count == 0)
            throw new UsageException($"No region '{regionName}'. Regions: {string.Join(", ", recording.Manifest.Regions.Select(r => $"'{r.Name}'"))}");

        var snapshots = new Dictionary<int, Snapshot>();
        byte At(int tick, uint address)
        {
            if (!snapshots.TryGetValue(tick, out var snapshot))
                snapshots[tick] = snapshot = recording[tick];
            return snapshot.Read(address, 1)[0];
        }

        var candidates = regions
            .SelectMany(r => Enumerable.Range(0, (int)PackYaml.ParseNumber(r.Size)).Select(offset => PackYaml.ParseNumber(r.Start) + (uint)offset))
            .ToList();
        foreach (var (name, operands) in filters)
        {
            if (name == "--equals")
            {
                var (tick, value) = (RecordingCommand.Tick(recording, operands[0]), ParseByte(operands[1]));
                candidates = candidates.Where(a => At(tick, a) == value).ToList();
            }
            else
            {
                var (from, to) = (RecordingCommand.Tick(recording, operands[0]), RecordingCommand.Tick(recording, operands[1]));
                candidates = candidates.Where(a => Comparisons[name].Passes(At(from, a), At(to, a))).ToList();
            }
            output.WriteLine($"after {name} {string.Join(' ', operands)}: {candidates.Count} candidate(s)");
        }

        var points = new[] { ("start", 0) }
            .Concat((recording.Manifest.Markers ?? []).Select(m => (m.Label, m.Tick)))
            .Append(("end", recording.Count - 1))
            .ToList();
        output.WriteLine();
        output.WriteLine("address   " + string.Join("  ", points.Select(p => $"{p.Item1[..Math.Min(p.Item1.Length, 12)],12}")));
        foreach (var address in candidates.Take(show))
            output.WriteLine($"0x{address:X6}  " + string.Join("  ", points.Select(p => $"{At(p.Item2, address),12}")));
        if (candidates.Count > show)
            output.WriteLine($"... {candidates.Count - show} more (raise --show, or add filters)");
    }

    private static byte ParseByte(string text) =>
        PackYaml.ParseNumber(text) is var value and <= byte.MaxValue
            ? (byte)value
            : throw new UsageException($"{text} isn't a byte (0 to 255)");
}
