using System.CommandLine;
using System.Runtime.CompilerServices;
using Stgadgets.Cli.Providers;
using Stgadgets.Cli.Recordings;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Providers;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Cli.Tests.Recordings;

/// <summary><c>stgadgets recording record</c>, against a fake emulator running the fixture's test game.</summary>
public sealed class RecordCommandTests : IDisposable
{
    private readonly RecordingFixture _fixture = new();
    private readonly FakeEmulator _emulator = new();

    public RecordCommandTests()
    {
        // The fixture's pack, with settings for the fake emulator.
        Directory.CreateDirectory(Path.Combine(_fixture.Packs, "test", "providers"));
        File.WriteAllText(PackSettings, "ready: true");
    }

    private string PackSettings => Path.Combine(_fixture.Packs, "test", "providers", "fake.yaml");

    public void Dispose() => _fixture.Dispose();

    private string NewRecording => Path.Combine(_fixture.Packs, "test", "recordings", "new");

    private (int ExitCode, string Output, string Error) Record(params string[] args)
    {
        var providers = new Dictionary<string, ConnectProvider> { ["fake"] = endpoint => _emulator.Connect(endpoint) };
        var root = new RootCommand { new Command("recording") { RecordCommand.Create(providers) } };
        var (output, error) = (new StringWriter(), new StringWriter());
        var exitCode = root.Parse(["recording", "record", "--provider", "fake", .. args]).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public void Records_what_the_packs_map_reads_until_the_game_stops()
    {
        _emulator.Memory = [[1, 7], [1, 7], [2, 7]];

        var (exitCode, output, error) = Record(NewRecording);

        Assert.Equal((0, ""), (exitCode, error));
        Assert.Contains("3 ticks", output);
        var recording = Recording.Load(NewRecording);
        Assert.Equal(new RecordingContent("test", "fake_core", "fake"), recording.Manifest.Content);
        Assert.Equal(new RecordingRegion("ram_100", "0x100", "0x1", "ram_100.bin"), Assert.Single(recording.Manifest.Regions));
        Assert.Equal([1, 1, 2], recording.Snapshots.Select(s => s.Read(0x100, 1)[0]));
    }

    [Fact]
    public void Records_the_ranges_asked_for()
    {
        _emulator.Memory = [[1, 7]];

        Assert.Equal(0, Record(NewRecording, "--range", "work=0x100:2").ExitCode);

        Assert.Equal(new RecordingRegion("work", "0x100", "0x2", "work.bin"), Assert.Single(Recording.Load(NewRecording).Manifest.Regions));
    }

    [Fact]
    public void Marks_a_tick_when_a_MARK_file_appears_and_logs_watched_values()
    {
        _emulator.Memory = [[1, 7], [1, 7], [2, 7]];
        _emulator.OnTick = tick =>
        {
            if (tick == 1)
                File.WriteAllText(Path.Combine(NewRecording, "MARK"), "x goes up\n");
        };

        Assert.Equal(0, Record(NewRecording, "--range", "0x100:2", "--watch", "x", "--watch", "0x101:u8:y").ExitCode);

        Assert.Equal([new RecordingMarker(1, "x goes up")], Recording.Load(NewRecording).Manifest.Markers);
        var log = File.ReadAllLines(Path.Combine(NewRecording, "changes.log"));
        Assert.EndsWith("tick      0  x 1, y 7", log[1]);
        Assert.EndsWith("tick      1  MARK x goes up", log[2]);
        Assert.EndsWith("tick      2  x 1->2", log[3]);
    }

    [Fact]
    public void Stops_when_a_STOP_file_appears()
    {
        _emulator.Memory = [[1, 7], [1, 7], [2, 7], [2, 7]];
        _emulator.OnTick = tick =>
        {
            if (tick == 1)
                File.WriteAllText(Path.Combine(NewRecording, "STOP"), "");
        };

        Assert.Contains("(StopFile)", Record(NewRecording).Output);

        Assert.Equal(2, Recording.Load(NewRecording).Count);
    }

    [Fact]
    public void Says_when_the_provider_cant_start_and_leaves_nothing_behind()
    {
        File.Delete(PackSettings);

        var (exitCode, output, error) = Record(NewRecording);

        Assert.Equal((1, ""), (exitCode, output));
        Assert.StartsWith("fake: no settings for fake_core. Give --settings, or add the settings to the pack's providers/fake.yaml", error);
        Assert.False(Directory.Exists(NewRecording));
    }

    [Fact]
    public void Takes_provider_settings_from_a_file()
    {
        File.Move(PackSettings, Path.Combine(_fixture.Packs, "elsewhere.yaml"));
        _emulator.Memory = [[1, 7]];

        Assert.Equal(0, Record(NewRecording, "--settings", Path.Combine(_fixture.Packs, "elsewhere.yaml")).ExitCode);
    }

    [Fact]
    public void Connects_to_the_endpoint_given()
    {
        _emulator.Memory = [[1, 7]];

        Assert.Equal(0, Record(NewRecording, "--endpoint", "somewhere:1234").ExitCode);

        Assert.Equal("somewhere:1234", _emulator.Endpoint);
    }

    [Fact]
    public void Only_takes_known_providers()
    {
        var root = new RootCommand { new Command("recording") { RecordCommand.Create() } };

        var errors = root.Parse(["recording", "record", NewRecording, "--provider", "mame"]).Errors;

        Assert.Contains(errors, e => e.Message.Contains("retroarch"));
    }

    [Fact]
    public void Wont_watch_memory_it_isnt_recording() =>
        Assert.Equal("--watch 0x180 (0x180) is outside the memory being recorded\n", Record(NewRecording, "--watch", "0x180").Error);

    [Fact]
    public void Wont_overwrite_a_recording() =>
        Assert.EndsWith("already exists\n", Record(_fixture.Recording).Error);

    /// <summary>
    /// Plays <see cref="Memory"/> from 0x100, one array per tick, then ends the stream as if the game had changed. Like
    /// a real provider it refuses to stream without its settings, and it calls <see cref="OnTick"/> before each
    /// snapshot.
    /// </summary>
    private sealed class FakeEmulator : IEmulatorProvider
    {
        public byte[][] Memory { get; set; } = [];

        public Action<int>? OnTick { get; set; }

        /// <summary>The endpoint it was connected to, if one was given.</summary>
        public string? Endpoint { get; private set; }

        public FakeEmulator Connect(string? endpoint)
        {
            Endpoint = endpoint;
            return this;
        }

        public string Id => "fake";

        public EmulatorProcessInfo? Process => null;

        public Task<ContentInfo?> GetContentAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ContentInfo?>(new ContentInfo("test", "fake_core"));

        public async IAsyncEnumerable<Snapshot> StreamAsync(
            ContentInfo content, ReadPlan plan, ProviderSettings? settings, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (settings is null)
                throw new NotSupportedException($"no settings for {content.System}");

            for (var tick = 0; tick < Memory.Length; tick++)
            {
                await Task.Yield();
                var memory = new MemoryBlock(new AddressRange(0x100, Memory[tick].Length), Memory[tick]);
                var blocks = plan.Ranges.Select(r => new MemoryBlock(r, memory.Data.Slice((int)(r.Start - 0x100), r.Length))).ToList();
                OnTick?.Invoke(tick);   // before the snapshot arrives, as when someone marks during a frame
                yield return new Snapshot(null, TimeSpan.FromMilliseconds(16.7 * tick), blocks);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
