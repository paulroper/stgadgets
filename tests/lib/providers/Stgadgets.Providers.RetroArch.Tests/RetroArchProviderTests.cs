using Stgadgets.Core.Memory;
using Stgadgets.Core.Providers;

namespace Stgadgets.Providers.RetroArch.Tests;

/// <summary>Streaming from <see cref="FakeRetroArch"/>, and what happens when it stops answering.</summary>
public sealed class RetroArchProviderTests : IDisposable
{
    private const int RetroArchPid = 4242;

    // Longer than MaxConsecutiveTimeouts × the 250 ms reply timeout, after which a stream used to end.
    private static readonly TimeSpan LongStall = TimeSpan.FromSeconds(3.5);

    private static readonly ContentInfo Batrider = new("batriderj", "fb_alpha", null);
    private static readonly ReadPlan Plan = new([new AddressRange(0x2100, 0x10)]);
    private static readonly ProviderSettings Settings = new("providers/retroarch.yaml", """
        schema: 1
        cores:
          fb_alpha: { ram_base: 0x2000, ram_size: 0x2000 }
        """);

    private readonly FakeRetroArch _retroArch = new("PLAYING fb_alpha,batriderj,crc32=6478f475");
    private bool _running = true;

    public void Dispose() => _retroArch.Dispose();

    private RetroArchProvider Provider() => new(_retroArch.Endpoint)
    {
        FindProcess = () => new EmulatorProcessInfo("RetroArch", RetroArchPid),
        IsRunning = pid => pid == RetroArchPid && _running,
    };

    [Fact]
    public async Task Streams_the_planned_memory()
    {
        await using var provider = Provider();
        Assert.Equal(Batrider with { ChipCrcs = null }, await provider.GetContentAsync(CancellationToken.None));

        var snapshot = await FirstSnapshotAsync(provider.StreamAsync(Batrider, Plan, Settings, CancellationToken.None).GetAsyncEnumerator());

        Assert.Equal(Plan.Ranges, snapshot.Blocks.Select(block => block.Range));
        Assert.Equal(new EmulatorProcessInfo("RetroArch", RetroArchPid), provider.Process);
    }

    [Fact]
    public async Task Waits_out_a_stall_while_RetroArch_is_running()
    {
        await using var provider = Provider();
        await provider.GetContentAsync(CancellationToken.None);
        await using var stream = provider.StreamAsync(Batrider, Plan, Settings, CancellationToken.None).GetAsyncEnumerator();
        await FirstSnapshotAsync(stream);

        _retroArch.Stalled = true;
        var next = stream.MoveNextAsync().AsTask();
        await Task.Delay(LongStall);
        Assert.False(next.IsCompleted);
        Assert.NotNull(provider.Process);   // so the overlay keeps following RetroArch's window

        _retroArch.Stalled = false;
        Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Gives_up_on_a_stall_once_RetroArch_has_gone()
    {
        await using var provider = Provider();
        await provider.GetContentAsync(CancellationToken.None);
        await using var stream = provider.StreamAsync(Batrider, Plan, Settings, CancellationToken.None).GetAsyncEnumerator();
        await FirstSnapshotAsync(stream);

        _retroArch.Stalled = true;
        _running = false;

        await Assert.ThrowsAsync<TimeoutException>(() => stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Reports_no_process_when_RetroArch_never_answers_and_isnt_running()
    {
        _retroArch.Stalled = true;
        _running = false;
        await using var provider = Provider();

        await Assert.ThrowsAsync<TimeoutException>(() => provider.GetContentAsync(CancellationToken.None));

        Assert.Null(provider.Process);
    }

    private static async Task<Snapshot> FirstSnapshotAsync(IAsyncEnumerator<Snapshot> stream)
    {
        Assert.True(await stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        return stream.Current;
    }
}
