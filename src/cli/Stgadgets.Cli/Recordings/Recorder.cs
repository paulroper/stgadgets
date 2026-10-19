using System.Diagnostics;
using System.Globalization;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Providers;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Recordings;

/// <summary>A value to log whenever it changes while recording: an address read as a type, or a map variable.</summary>
internal sealed record Watch(string Name, Variable Variable);

/// <summary>Why a recording stopped.</summary>
internal enum StopReason { Asked, TimeUp, StopFile, GameChanged, EmulatorStopped }

/// <summary>
/// Records snapshots from a provider into a new recording until it's stopped. While it runs, a file named MARK in the
/// recording's folder adds a marker (its contents are the label), and a file named STOP ends it. Values being watched
/// are logged to changes.log as they change. The recording's folder is created with the first snapshot, so a provider
/// that refuses to start (say, for want of settings) leaves nothing behind.
/// </summary>
internal sealed class Recorder(IEmulatorProvider provider, ContentInfo content, ProviderSettings? settings, Endianness endianness)
{
    public const string MarkFile = "MARK";
    public const string StopFile = "STOP";

    /// <summary>What was recorded: ticks, markers, and the time between snapshots (about a frame each).</summary>
    public sealed record Summary(int Ticks, int Markers, TimeSpan Duration, double IntervalP50Ms, double IntervalP95Ms, StopReason Reason);

    /// <param name="started">Called once the first snapshot arrives and the recording exists.</param>
    public async Task<Summary> RecordAsync(
        string directory, IReadOnlyList<(string Name, AddressRange Range)> regions, IReadOnlyList<Watch> watches,
        TimeSpan limit, string? notes, Action started, CancellationToken cancellationToken)
    {
        RecordingWriter? writer = null;
        StreamWriter? log = null;
        var (markPath, stopPath) = (Path.Combine(directory, MarkFile), Path.Combine(directory, StopFile));
        var intervals = new List<double>();
        var previous = new Dictionary<string, object?>();
        var lastTimestamp = (TimeSpan?)null;
        var reason = StopReason.GameChanged;
        var markers = 0;
        var clock = Stopwatch.StartNew();
        try
        {
            await foreach (var snapshot in provider.StreamAsync(content, new ReadPlan(regions.Select(r => r.Range).ToList()), settings, cancellationToken))
            {
                if (writer is null)
                {
                    var capturedAt = DateTimeOffset.UtcNow;
                    writer = new RecordingWriter(directory, regions, new RecordingContent(content.Romset, content.System, provider.Id), capturedAt)
                    {
                        Notes = notes,
                    };
                    log = new StreamWriter(Path.Combine(directory, "changes.log")) { AutoFlush = true };
                    log.WriteLine($"# {capturedAt:O} {provider.Id}: {content.System}, {content.Romset}");
                    started();
                }

                writer.Write(snapshot);
                var tick = writer.Count - 1;
                var at = $"{snapshot.Timestamp.TotalSeconds,9:F3}s tick {tick,6}  ";
                if (lastTimestamp is { } last)
                    intervals.Add((snapshot.Timestamp - last).TotalMilliseconds);
                lastTimestamp = snapshot.Timestamp;

                if (File.Exists(markPath))
                {
                    var label = (await File.ReadAllTextAsync(markPath, CancellationToken.None)).Trim();
                    File.Delete(markPath);
                    writer.Mark(label.Length > 0 ? label : $"mark {tick}");
                    markers++;
                    log!.WriteLine($"{at}MARK {label}");
                }

                var changes = new List<string>();
                foreach (var (name, variable) in watches)
                {
                    var value = MapDecoder.Decode(variable, snapshot.Read(variable.Address, variable.Type.Size), endianness);
                    if (!previous.TryGetValue(name, out var before))
                        changes.Add($"{name} {Format(value)}");
                    else if (!Equals(before, value))
                        changes.Add($"{name} {Format(before)}->{Format(value)}");
                    previous[name] = value;
                }
                if (changes.Count > 0)
                    log!.WriteLine(at + string.Join(", ", changes));

                if (File.Exists(stopPath))
                {
                    File.Delete(stopPath);
                    reason = StopReason.StopFile;
                    break;
                }
                if (clock.Elapsed >= limit)
                {
                    reason = StopReason.TimeUp;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            reason = StopReason.Asked;
        }
        catch (TimeoutException ex) when (writer is not null)   // before the first snapshot, there's nothing to keep
        {
            reason = StopReason.EmulatorStopped;
            log!.WriteLine($"# stopped: {ex.Message}");
        }
        finally
        {
            writer?.Dispose();
        }

        intervals.Sort();
        double Percentile(double p) => intervals.Count == 0 ? 0 : intervals[(int)(p * (intervals.Count - 1))];
        var summary = new Summary(writer?.Count ?? 0, markers, clock.Elapsed, Percentile(0.5), Percentile(0.95), reason);
        log?.WriteLine($"# done ({reason}): {summary.Ticks} ticks, interval p50 {summary.IntervalP50Ms:F1} ms, p95 {summary.IntervalP95Ms:F1} ms");
        log?.Dispose();
        return summary;
    }

    private static string Format(object? value) => value switch
    {
        null => "-",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };
}
