using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Providers;

namespace Stgadgets.Providers.RetroArch;

/// <summary>
/// Reads memory through RetroArch's network commands. Polling, not frame-synced: RetroArch answers once per frame,
/// so polling back-to-back yields roughly one snapshot per frame.
/// </summary>
public sealed class RetroArchProvider(IPEndPoint endpoint) : IEmulatorProvider
{
    private static readonly TimeSpan StatusCheckInterval = TimeSpan.FromSeconds(2);
    private const int MaxConsecutiveTimeouts = 10;

    private const string AppName = "RetroArch";

    private readonly RetroArchClient _client = new(endpoint, TimeSpan.FromMilliseconds(250));
    private volatile EmulatorProcessInfo? _process;

    public RetroArchProvider() : this(new IPEndPoint(IPAddress.Loopback, 55355)) { }

    public string Id => "retroarch";

    public EmulatorProcessInfo? Process => _process;

    public async Task<ContentInfo?> GetContentAsync(CancellationToken cancellationToken)
    {
        RetroArchStatus status;
        try
        {
            status = await _client.GetStatusAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            _process = null;
            throw;
        }

        if (_process?.ProcessId is not { } pid || !IsRunning(pid))
            _process = FindProcess();
        return status.State == "CONTENTLESS" ? null : new ContentInfo(status.Content, status.System);
    }

    /// <summary>
    /// Network commands don't say which process answered, so look RetroArch up by name. With several copies running
    /// this picks one arbitrarily; good enough until it isn't.
    /// </summary>
    private static EmulatorProcessInfo FindProcess()
    {
        var process = System.Diagnostics.Process.GetProcessesByName(AppName)
            .Concat(System.Diagnostics.Process.GetProcessesByName(AppName.ToLowerInvariant()))   // Windows/Linux
            .FirstOrDefault();
        return new EmulatorProcessInfo(AppName, process?.Id);
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            return !System.Diagnostics.Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async IAsyncEnumerable<Snapshot> StreamAsync(
        ContentInfo content, ReadPlan plan, ProviderSettings? settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var profile = CoreProfile.Find(settings, content.System)
            ?? throw new NotSupportedException($"The pack for '{content.Romset}' has no RetroArch core profile for '{content.System}' yet");

        // One buffer per range, split into chunks RetroArch can reply to in a single datagram.
        var readRanges = plan.Ranges.Select(profile.ToReadRange).ToArray();
        var buffers = readRanges.Select(r => new byte[r.Length]).ToArray();
        var reads = readRanges
            .SelectMany((range, i) => Chunks(range.Length).Select(c =>
                (Offset: profile.ToOffset(range) + (uint)c.Start, Destination: buffers[i].AsMemory(c.Start, c.Length))))
            .ToList();

        var clock = Stopwatch.StartNew();
        var lastStatusCheck = TimeSpan.Zero;
        var timeouts = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (clock.Elapsed - lastStatusCheck > StatusCheckInterval)
            {
                lastStatusCheck = clock.Elapsed;
                if (await GetContentAsync(cancellationToken) != content)
                    yield break;
            }

            try
            {
                await _client.ReadCoreRamAsync(reads, cancellationToken);
                timeouts = 0;
            }
            catch (TimeoutException) when (++timeouts < MaxConsecutiveTimeouts)
            {
                continue;
            }

            // Copy, since the buffers are reused for the next read.
            var blocks = plan.Ranges.Select((range, i) =>
            {
                profile.Unswap(buffers[i]);
                var skip = (int)(range.Start - readRanges[i].Start);
                return new MemoryBlock(range, buffers[i].AsSpan(skip, range.Length).ToArray());
            }).ToList();
            yield return new Snapshot(EmulatedFrame: null, clock.Elapsed, blocks);
        }
    }

    private static IEnumerable<(int Start, int Length)> Chunks(int length)
    {
        for (var start = 0; start < length; start += RetroArchClient.MaxReadLength)
            yield return (start, Math.Min(RetroArchClient.MaxReadLength, length - start));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
