using System.CommandLine;
using System.Text.RegularExpressions;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;
using Stgadgets.Providers;

namespace Stgadgets.Cli.Peek;

/// <summary>
/// <c>stgadgets peek</c>: values from live play, now. Everything about recordings is under <c>stgadgets recording</c>,
/// so neither subcommand reads one.
/// </summary>
internal static class PeekCommand
{
    /// <param name="providers">The providers <c>peek memory</c> chooses from, by ID; tests add a fake.</param>
    /// <param name="server">The server <c>peek values</c> asks; tests run their own.</param>
    public static Command Create(IReadOnlyDictionary<string, ConnectProvider>? providers = null, Uri? server = null) =>
        new("peek", "Read values from the game being played now: its memory, or what the server publishes.")
        {
            MemoryCommand.Create(providers),
            ValuesCommand.Create(server),
        };

    public static Argument<string[]> Names(string what) => new("names")
    {
        Description = $"{what} to read: a name, an array entry's field (enemies[3].hp), or a pattern where * matches anything " +
            "('p1.*'). Default: all of them except arrays.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    /// <summary>
    /// Writes the values <paramref name="names"/> pick, one a line, with arrays one entry's field a line.
    /// </summary>
    /// <param name="source">What the values are from, for errors: "ddonpach's map".</param>
    public static void Write(Dictionary<string, object?> values, string[] names, string source, TextWriter output)
    {
        var picked = Pick(values, names, source).SelectMany(Expand).ToList();
        var width = picked.Max(p => p.Name.Length) + 2;
        foreach (var (name, value) in picked)
            output.WriteLine($"{name.PadRight(width)}{PackYaml.FormatValue(value)}");
    }

    private static IEnumerable<(string Name, object? Value)> Pick(Dictionary<string, object?> values, string[] names, string source)
    {
        if (names.Length == 0)
            return values.Where(v => v.Value is not ArrayValue).Select(v => (v.Key, v.Value));

        return names.SelectMany(name =>
        {
            if (name.Contains('*', StringComparison.Ordinal))
            {
                var pattern = new Regex($"^{Regex.Escape(name).Replace(@"\*", ".*", StringComparison.Ordinal)}$");
                var matches = values.Where(v => pattern.IsMatch(v.Key)).Select(v => (v.Key, v.Value)).ToList();
                return matches.Count > 0 ? matches : throw new UsageException($"Nothing in {source} matches '{name}'");
            }
            return ValuePath.Parse(name).TryGet(values, out var value)
                ? [(name, value)]
                : throw new UsageException($"{source} has no '{name}'");
        });
    }

    /// <summary>An array as one line per entry's field, the way they're named elsewhere: <c>enemies[3].hp</c>.</summary>
    private static IEnumerable<(string Name, object? Value)> Expand((string Name, object? Value) picked) =>
        picked.Value is ArrayValue array
            ? array.SelectMany((entry, index) => entry.Select(field => ($"{picked.Name}[{index}].{field.Key}", field.Value)))
            : [picked];
}
