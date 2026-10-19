using System.Buffers.Binary;
using System.Text.Json.Nodes;
using static Stgadgets.Cli.Tests.Recordings.RecordingFixture;

namespace Stgadgets.Cli.Tests.Recordings;

/// <summary><c>stgadgets recording</c>, on <see cref="RecordingFixture"/>.</summary>
public sealed class RecordingCommandTests : IDisposable
{
    private readonly RecordingFixture _fixture = new();

    private string Recording => _fixture.Recording;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Markers_lists_the_ticks_and_markers() =>
        Assert.Equal((0, "4 ticks\n       2  x goes up\n", ""), Run("recording", "markers", Recording));

    [Fact]
    public void Values_shows_map_and_derived_values_at_ticks_and_markers() =>
        Assert.Equal(
            (0, """
                {
                  "start": {
                    "tick": 0,
                    "values": {
                      "x": 1,
                      "x.doubled": 2
                    }
                  },
                  "x goes up": {
                    "tick": 2,
                    "values": {
                      "x": 2,
                      "x.doubled": 4
                    }
                  }
                }

                """, ""),
            Run("recording", "values", Recording, "--at", "start", "--at", "x goes up"));

    [Fact]
    public void Values_can_show_only_matching_names()
    {
        var (exitCode, output, _) = Run("recording", "values", Recording, "--at", "end", "--only", "*.doubled");

        Assert.Equal(0, exitCode);
        Assert.Contains("\"x.doubled\": 4", output);
        Assert.DoesNotContain("\"x\":", output);
    }

    [Fact]
    public void Points_can_be_a_unique_prefix_of_a_marker() =>
        Assert.Contains("\"tick\": 2", Run("recording", "values", Recording, "--at", "x goes").Output);

    [Fact]
    public void Lists_the_markers_when_a_point_doesnt_exist() =>
        Assert.Equal((1, "", "No tick or marker 'x goes down'. Markers: 'x goes up'\n"), Run("recording", "values", Recording, "--at", "x goes down"));

    [Fact]
    public void Says_when_a_folder_isnt_a_recording()
    {
        var (exitCode, _, error) = Run("recording", "markers", _fixture.Packs);

        Assert.Equal(1, exitCode);
        Assert.EndsWith("isn't a recording: it has no manifest.json\n", error);
    }

    [Fact]
    public void Search_applies_filters_in_order_and_shows_the_candidates() =>
        Assert.Equal(
            (0, """
                after --changed start end: 2 candidate(s)
                after --decreased start 1: 1 candidate(s)

                address          start     x goes up           end
                0x000101             5             4             9

                """, ""),
            Run("recording", "search", Recording, "--changed", "start", "end", "--decreased", "start", "1"));

    [Fact]
    public void Search_by_value() =>
        Assert.Contains("after --equals x goes up 0x02: 1 candidate(s)", Run("recording", "search", Recording, "--equals", "x goes up", "0x02").Output);

    [Fact]
    public void History_of_an_address_shows_each_change_with_markers() =>
        Assert.Equal(
            (0, "tick      0         5\ntick      1         4\ntick      3         9\n", ""),
            Run("recording", "history", Recording, "0x101"));

    [Fact]
    public void History_reads_an_address_as_a_map_type() =>
        // 0x100-0x101 little-endian: 0x0501, 0x0401, 0x0402, 0x0902
        Assert.Equal(
            (0, "tick      0      1281\ntick      1      1025\ntick      2      1026   <- x goes up\ntick      3      2306\n", ""),
            Run("recording", "history", Recording, "0x100", "--type", "u16"));

    [Fact]
    public void History_of_a_derived_value() =>
        Assert.Equal((0, "tick      0         2\ntick      2         4   <- x goes up\n", ""), Run("recording", "history", Recording, "x.doubled"));

    [Fact]
    public void Mark_adds_a_marker_and_moves_one_with_the_same_label()
    {
        Assert.Equal(0, Run("recording", "mark", Recording, "3", "the end").ExitCode);
        Assert.Equal(0, Run("recording", "mark", Recording, "1", "x goes up").ExitCode);

        Assert.Equal("4 ticks\n       1  x goes up\n       3  the end\n", Run("recording", "markers", Recording).Output);
        Assert.Equal("2026-10-19T20:00:00Z", Manifest(Recording)["capturedAt"]!.GetValue<string>());
    }

    [Fact]
    public void Trim_copies_a_slice_with_its_markers_and_rebased_timestamps()
    {
        var trimmed = Path.Combine(_fixture.Packs, "test", "recordings", "trimmed");

        Assert.Equal(0, Run("recording", "trim", Recording, "1", "x goes up", "--out", trimmed).ExitCode);

        Assert.Equal([1, 4, 2, 4], File.ReadAllBytes(Path.Combine(trimmed, "ram.bin")));
        var ticks = File.ReadAllBytes(Path.Combine(trimmed, "ticks.bin"));
        Assert.Equal([0L, 1000L], [BinaryPrimitives.ReadInt64LittleEndian(ticks), BinaryPrimitives.ReadInt64LittleEndian(ticks.AsSpan(16))]);
        Assert.Equal("2 ticks\n       1  x goes up\n", Run("recording", "markers", trimmed).Output);
        Assert.Equal("Made up. Trimmed to ticks 1..2 of run.", Manifest(trimmed)["notes"]!.GetValue<string>());
    }

    [Fact]
    public void Trim_wont_overwrite_a_folder() =>
        Assert.Equal(1, Run("recording", "trim", Recording, "start", "end", "--out", _fixture.Packs).ExitCode);

    private static JsonNode Manifest(string recording) => JsonNode.Parse(File.ReadAllText(Path.Combine(recording, "manifest.json")))!;
}
