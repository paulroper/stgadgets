using System.CommandLine;
using System.Globalization;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Recordings;

/// <summary>
/// <c>stgadgets recording history</c>: every distinct value of an address, or of a map variable or derived value,
/// over the recording, with the ticks it changed at.
/// </summary>
internal static class HistoryCommand
{
    public static Command Create()
    {
        var recording = RecordingCommand.Argument();
        var target = new Argument<string>("address-or-name")
        {
            Description = "An address (0x hex or decimal), or the name of a map variable or derived value in the recording's pack, or an array entry's field: enemies[3].hp.",
        };
        var type = new Option<string>("--type")
        {
            Description = "How to read an address, as in map.yaml: u8, u16, s16be, bcd2, … (default u8).",
            DefaultValueFactory = _ => "u8",
        };
        var endianness = new Option<Endianness?>("--endianness")
        {
            Description = "Byte order for an address's type when it doesn't say: little or big. Default: the pack's map, else little.",
        };
        var command = new Command(
            "history",
            "Every distinct value of an address, or of a map variable or derived value, over the recording, with the ticks it changed at.")
        {
            recording, target, type, endianness,
        };
        command.SetAction(StgadgetsCommand.Handle(result =>
        {
            var directory = result.GetRequiredValue(recording);
            var loaded = RecordingCommand.Load(directory);
            var pack = RecordingCommand.PackOf(directory);
            var what = result.GetRequiredValue(target);
            var values = IsAddress(what)
                ? AddressValues(loaded, PackYaml.ParseNumber(what), VariableType.Parse(result.GetRequiredValue(type)),
                    result.GetValue(endianness) ?? pack?.Map.Endianness ?? Endianness.Little)
                : NamedValues(loaded, pack ?? RecordingCommand.RequirePackOf(directory), what);
            Write(loaded, values, result.InvocationConfiguration.Output);
        }));
        return command;
    }

    private static bool IsAddress(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || text.All(char.IsAsciiDigit);

    private static IEnumerable<object?> AddressValues(Recording recording, uint address, VariableType type, Endianness endianness) =>
        recording.Snapshots.Select(snapshot => MapDecoder.Decode(type, snapshot.Read(address, type.Size), endianness));

    private static IEnumerable<object?> NamedValues(Recording recording, Pack pack, string name)
    {
        var path = ValuePath.Parse(name);
        foreach (var tick in RecordingReplay.Run(recording, new MapDecoder(pack.Map), pack.Derived))
        {
            yield return path.TryGet(tick.Values, out var value)
                ? value
                : throw new UsageException($"{pack.Id} has no map variable or derived value '{name}'");
        }
    }

    private static void Write(Recording recording, IEnumerable<object?> values, TextWriter output)
    {
        var tick = 0;
        object? previous = null;
        foreach (var value in values)
        {
            if (tick == 0 || !Equals(value, previous))
            {
                var markers = string.Join(", ", RecordingCommand.MarkersAt(recording, tick));
                output.WriteLine($"tick {tick,6}  {Format(value),8}{(markers.Length > 0 ? $"   <- {markers}" : "")}");
                previous = value;
            }
            tick++;
        }
    }

    private static string Format(object? value) => value is double d ? d.ToString("0.####", CultureInfo.InvariantCulture) : PackYaml.FormatValue(value);
}
