using System.Diagnostics;
using System.Runtime.CompilerServices;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Providers.Replay;

/// <summary>
/// Plays a recording back as if it were an emulator (docs/providers.md#replay-provider), so the server, the overlay and
/// screenshots work against real play with nobody playing. The endpoint is the recording's folder:
/// <list type="bullet">
/// <item><c>&lt;folder&gt;</c> plays it at its original speed, then ends, so the server starts it again.</item>
/// <item><c>&lt;folder&gt;#&lt;point&gt;</c> plays up to the point (a tick, a marker label, "start" or "end") as fast as it
/// can, so values that build up over time are right, then holds there until stopped.</item>
/// </list>
/// </summary>
public sealed class ReplayProvider : IEmulatorProvider
{
    private readonly Recording _recording;
    private readonly int? _holdAt;

    /// <param name="endpoint">The recording's folder, optionally followed by <c>#</c> and the point to hold at.</param>
    /// <param name="baseDirectory">What a relative folder is relative to; the current directory by default.</param>
    /// <exception cref="ArgumentException">The folder isn't a recording, or the point isn't in it.</exception>
    public ReplayProvider(string endpoint, string? baseDirectory = null)
    {
        var (folder, point) = endpoint.Split('#', 2) is [var f, var p] ? (f, p) : (endpoint, null);
        var directory = Path.GetFullPath(folder, baseDirectory ?? Directory.GetCurrentDirectory());
        if (!File.Exists(Path.Combine(directory, "manifest.json")))
            throw new ArgumentException($"{directory} isn't a recording: it has no manifest.json", nameof(endpoint));

        _recording = Recording.Load(directory);
        Source = point is null ? directory : $"{directory}#{point}";
        if (point is not null)
        {
            _holdAt = (point == "end" ? _recording.Count - 1 : _recording.FindTick(point)) ?? throw new ArgumentException(
                $"No tick or marker '{point}' in {directory}. Markers: {string.Join(", ", (_recording.Manifest.Markers ?? []).Select(m => $"'{m.Label}'"))}",
                nameof(endpoint));
        }
    }

    public string Id => "replay";

    /// <summary>What's being played, for messages.</summary>
    public string Source { get; }

    /// <summary>A recording has no window for the overlay to follow.</summary>
    public EmulatorProcessInfo? Process => null;

    /// <summary>The game the recording was made from, as its manifest says.</summary>
    public Task<ContentInfo?> GetContentAsync(CancellationToken cancellationToken) =>
        Task.FromResult<ContentInfo?>(new ContentInfo(_recording.Manifest.Content.Romset, _recording.Manifest.Content.Core));

    public async IAsyncEnumerable<Snapshot> StreamAsync(
        ContentInfo content, ReadPlan plan, ProviderSettings? settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var regions = _recording.Manifest.Regions;
        var recorded = regions.Select(r => new AddressRange(PackYaml.ParseNumber(r.Start), (int)PackYaml.ParseNumber(r.Size))).ToList();
        foreach (var range in plan.Ranges.Where(range => !recorded.Any(r => r.Contains(range.Start, range.Length))))
            throw new NotSupportedException($"The recording doesn't cover {range}, which the pack's map reads: record it with that range ({Source})");

        var clock = Stopwatch.StartNew();
        var last = _holdAt ?? _recording.Count - 1;
        var start = _recording.Count > 0 ? _recording[0].Timestamp : TimeSpan.Zero;
        for (var tick = 0; tick <= last; tick++)
        {
            var snapshot = _recording[tick];
            if (_holdAt is null && snapshot.Timestamp - start - clock.Elapsed is { Ticks: > 0 } wait)
                await Task.Delay(wait, cancellationToken);   // real time; fast-forwarding to a hold point doesn't wait
            cancellationToken.ThrowIfCancellationRequested();
            yield return snapshot;
        }

        if (_holdAt is not null)
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
