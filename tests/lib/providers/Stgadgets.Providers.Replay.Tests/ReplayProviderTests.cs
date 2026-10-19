using Stgadgets.Core.Memory;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Providers.Replay.Tests;

/// <summary>
/// The replay provider, on a recording of one byte at 0x100 that goes 1, 2, 3, 4, one tick every 20 ms, with a marker
/// at tick 2.
/// </summary>
public sealed class ReplayProviderTests : IDisposable
{
    private static readonly ReadPlan Plan = new([new AddressRange(0x100, 1)]);

    private readonly string _recording = Path.Combine(Directory.CreateTempSubdirectory("stgadgets-replay-").FullName, "run");

    public ReplayProviderTests()
    {
        using var writer = new RecordingWriter(
            _recording, [("ram", new AddressRange(0x100, 1))], new RecordingContent("test", "test_core", "retroarch"), DateTimeOffset.UnixEpoch);
        for (var tick = 0; tick < 4; tick++)
        {
            var block = new MemoryBlock(new AddressRange(0x100, 1), new[] { (byte)(tick + 1) });
            writer.Write(new Snapshot(null, TimeSpan.FromMilliseconds(20 * tick), [block]));
            if (tick == 2)
                writer.Mark("three");
        }
    }

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_recording)!, recursive: true);

    private static async Task<List<byte>> Values(ReplayProvider provider, int count, CancellationToken cancellationToken = default)
    {
        var content = (await provider.GetContentAsync(cancellationToken))!;
        var values = new List<byte>();
        await foreach (var snapshot in provider.StreamAsync(content, Plan, null, cancellationToken))
        {
            values.Add(snapshot.Read(0x100, 1)[0]);
            if (values.Count == count)
                break;
        }
        return values;
    }

    [Fact]
    public async Task Reports_the_game_the_recording_was_made_from() =>
        Assert.Equal(new("test", "test_core"), await new ReplayProvider(_recording).GetContentAsync(CancellationToken.None));

    [Fact]
    public async Task Plays_every_tick_at_the_recordings_speed_then_ends()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        Assert.Equal([1, 2, 3, 4], await Values(new ReplayProvider(_recording), count: 10));

        Assert.InRange(clock.ElapsedMilliseconds, 55, 1000);   // ticks at 0, 20, 40 and 60 ms
    }

    [Theory]
    [InlineData("three", new byte[] { 1, 2, 3 })]
    [InlineData("1", new byte[] { 1, 2 })]
    [InlineData("end", new byte[] { 1, 2, 3, 4 })]
    public async Task Fast_forwards_to_a_point_then_holds_there(string point, byte[] expected)
    {
        var provider = new ReplayProvider($"{_recording}#{point}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var values = new List<byte>();
        var content = (await provider.GetContentAsync(CancellationToken.None))!;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var snapshot in provider.StreamAsync(content, Plan, null, timeout.Token))
                values.Add(snapshot.Read(0x100, 1)[0]);
        });

        Assert.Equal(expected, values);
    }

    [Fact]
    public void Lists_the_markers_when_a_point_isnt_in_the_recording() =>
        Assert.Contains("Markers: 'three'", Assert.Throws<ArgumentException>(() => new ReplayProvider($"{_recording}#four")).Message);

    [Fact]
    public void Says_when_a_folder_isnt_a_recording() =>
        Assert.Contains("isn't a recording", Assert.Throws<ArgumentException>(() => new ReplayProvider(Path.GetDirectoryName(_recording)!)).Message);

    [Fact]
    public async Task Says_when_the_recording_doesnt_cover_what_the_map_reads()
    {
        var provider = new ReplayProvider(_recording);
        var content = (await provider.GetContentAsync(CancellationToken.None))!;

        var error = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in provider.StreamAsync(content, new ReadPlan([new AddressRange(0x100, 2)]), null, CancellationToken.None)) { }
        });

        Assert.Contains("doesn't cover 0x100..0x101", error.Message);
    }

    [Fact]
    public void Is_one_of_the_platforms_providers() =>
        Assert.IsType<ReplayProvider>(EmulatorProviders.Connect("replay", _recording));
}
