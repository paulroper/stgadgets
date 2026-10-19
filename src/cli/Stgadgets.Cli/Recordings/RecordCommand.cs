using System.CommandLine;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;
using Stgadgets.Providers;

namespace Stgadgets.Cli.Recordings;

/// <summary>
/// <c>stgadgets recording record</c>: record a game's memory from an emulator while someone plays, through one of
/// the platform's providers. The provider's settings (for RetroArch, the core profile) come from the pack's
/// providers/&lt;id&gt;.yaml, as they do for the server, or from <c>--settings</c>.
/// </summary>
internal static class RecordCommand
{
    /// <param name="providers">The providers to choose from, by ID; tests add a fake.</param>
    public static Command Create(IReadOnlyDictionary<string, ConnectProvider>? providers = null)
    {
        providers ??= EmulatorProviders.All;

        var output = new Argument<DirectoryInfo>("recording")
        {
            Description = "The recording's folder, to create: packs/<id>/recordings/<name> to use the pack's map and provider settings.",
        };
        var provider = new Option<string>("--provider")
        {
            Description = $"The emulator to record from: {string.Join(", ", providers.Keys)}.",
            Required = true,
        };
        provider.AcceptOnlyFromAmong([.. providers.Keys]);
        var endpoint = new Option<string>("--endpoint")
        {
            Description = "Where the emulator listens, in the provider's form (retroarch: host:port, default 127.0.0.1:55355).",
        };
        var settingsFile = new Option<FileInfo>("--settings")
        {
            Description = "The provider's settings, shaped like a pack's providers/<id>.yaml, for a game whose pack has none " +
                "yet (or to override them): for RetroArch, the core profile.",
        };
        var ranges = new Option<string[]>("--range")
        {
            Description = "Memory to record, as [name=]start:size, e.g. work_ram=0x2000:0x400. Repeat for several. " +
                "Default: what the pack's map reads.",
        };
        var watches = new Option<string[]>("--watch")
        {
            Description = "A value to log to changes.log whenever it changes: a map variable's name, or address[:type[:name]] " +
                "with a map.yaml type (default u8). Repeat for several.",
        };
        var seconds = new Option<double>("--seconds") { Description = "Stop after this long.", DefaultValueFactory = _ => 600 };
        var notes = new Option<string>("--notes") { Description = "Notes for the manifest: what happens in the recording." };
        var endianness = new Option<Endianness?>("--endianness")
        {
            Description = "Byte order for watched addresses whose type doesn't say. Default: the pack's map, else little.",
        };

        var command = new Command(
            "record",
            "Record memory from an emulator every frame while someone plays. Stop with Ctrl+C, after --seconds, or by creating " +
            "a file named STOP in the recording's folder; mark a moment by writing its label to a file named MARK there.")
        {
            output, provider, endpoint, settingsFile, ranges, watches, seconds, notes, endianness,
        };
        command.SetAction(StgadgetsCommand.HandleAsync(async (result, cancellationToken) =>
        {
            var directory = result.GetRequiredValue(output);
            if (directory.Exists)
                throw new UsageException($"{directory.FullName} already exists");
            var pack = RecordingCommand.PackOf(directory);
            var regions = Regions(result.GetValue(ranges) ?? [], pack);
            var byteOrder = result.GetValue(endianness) ?? pack?.Map.Endianness ?? Endianness.Little;
            var watched = (result.GetValue(watches) ?? []).Select(w => ParseWatch(w, pack)).ToList();
            foreach (var watch in watched.Where(w => !regions.Any(r => r.Range.Contains(w.Variable.Address, w.Variable.Type.Size))))
                throw new UsageException($"--watch {watch.Name} (0x{watch.Variable.Address:X}) is outside the memory being recorded");

            var console = result.InvocationConfiguration;
            var providerId = result.GetRequiredValue(provider);
            var settings = result.GetValue(settingsFile) is { } file
                ? new ProviderSettings(file.FullName, await File.ReadAllTextAsync(file.FullName, cancellationToken))
                : pack?.ProviderSettings.GetValueOrDefault(providerId);

            await using var emulator = providers[providerId](result.GetValue(endpoint));
            var content = await emulator.GetContentAsync(cancellationToken) ?? throw new UsageException($"{providerId}: no game loaded");
            if (pack is not null && !pack.Matches(content))
                console.Error.WriteLine($"warning: {providerId} is running '{content.Romset}', which isn't {pack.Id}");

            void Started()
            {
                var bytes = regions.Sum(r => r.Range.Length);
                console.Output.WriteLine(
                    $"Recording {string.Join(", ", regions.Select(r => $"{r.Name} {r.Range}"))} from {content.System}, {content.Romset}: " +
                    $"{bytes:N0} bytes a tick, about {bytes * 60 * 60 / 1e6:F1} MB a minute.");
                console.Output.WriteLine(
                    $"Stop with Ctrl+C or `touch {Path.Combine(directory.FullName, Recorder.StopFile)}`; " +
                    $"mark with `echo \"label\" > {Path.Combine(directory.FullName, Recorder.MarkFile)}`.");
            }

            var recorder = new Recorder(emulator, content, settings, byteOrder);
            Recorder.Summary summary;
            try
            {
                summary = await recorder.RecordAsync(
                    directory.FullName, regions, watched, TimeSpan.FromSeconds(result.GetValue(seconds)),
                    result.GetValue(notes) ?? $"Recorded by stgadgets from {providerId} ({content.System}).",
                    Started, cancellationToken);
            }
            catch (NotSupportedException ex)   // the provider can't read this game as set up, e.g. no core profile
            {
                throw new UsageException(
                    $"{providerId}: {ex.Message}. Give --settings, or add the settings to the pack's providers/{providerId}.yaml (docs/providers.md)");
            }
            if (summary.Ticks == 0)
                throw new UsageException($"{providerId} sent nothing ({summary.Reason}), so there's no recording");
            console.Output.WriteLine(
                $"{summary.Ticks} ticks in {summary.Duration.TotalSeconds:F1} s ({summary.Reason}); " +
                $"interval p50 {summary.IntervalP50Ms:F1} ms, p95 {summary.IntervalP95Ms:F1} ms; {summary.Markers} marker(s)");
        }));
        return command;
    }

    private static List<(string Name, AddressRange Range)> Regions(string[] ranges, Pack? pack)
    {
        if (ranges.Length == 0)
        {
            if (pack is null)
                throw new UsageException("Give --range, or record into a pack (packs/<id>/recordings/<name>) to record what its map reads");
            return ReadPlan.Create(pack.Map.Variables.Select(v => v.Range)).Ranges.Select(r => ($"ram_{r.Start:X}", r)).ToList();
        }

        return ranges.Select((text, i) =>
        {
            var (name, spec) = text.Split('=', 2) is [var n, var s] ? (n, s) : (ranges.Length == 1 ? "ram" : $"ram{i + 1}", text);
            if (spec.Split(':') is not [var start, var size])
                throw new UsageException($"--range {text}: expected [name=]start:size, e.g. work_ram=0x2000:0x400");
            return (name, new AddressRange(Number(start), (int)Number(size)));
        }).ToList();
    }

    private static Watch ParseWatch(string text, Pack? pack)
    {
        if (!IsNumber(text.Split(':')[0]))
        {
            var variable = pack?.Map.Variables.FirstOrDefault(v => v.Name == text)
                ?? throw new UsageException($"--watch {text}: not an address, or a variable in {(pack is null ? "a pack's map" : $"{pack.Id}'s map")}");
            return new Watch(text, variable);
        }

        var parts = text.Split(':');
        var address = Number(parts[0]);
        var type = parts.Length > 1 ? VariableType.Parse(parts[1]) : VariableType.Parse("u8");
        var name = parts.Length > 2 ? parts[2] : $"0x{address:X}";
        return new Watch(name, new Variable(name, address, type));
    }

    private static bool IsNumber(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || (text.Length > 0 && text.All(char.IsAsciiDigit));

    private static uint Number(string text) =>
        IsNumber(text) ? PackYaml.ParseNumber(text) : throw new UsageException($"'{text}' isn't a number (decimal or 0x hex)");
}
