using System.Buffers.Binary;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Core.Tests;

/// <summary>The harness itself, on a three-tick recording of one byte at 0x100 that goes 1, 1, 2.</summary>
public sealed class ExpectedValuesTests : IDisposable
{
    private readonly TempDirectory _recording = new();
    private readonly MapDecoder _decoder = new(MemoryMapLoader.Parse("""
        schema: 1
        system: { endianness: little }
        variables:
          x: u8 @ 0x100
        """));

    public ExpectedValuesTests()
    {
        _recording.Write("manifest.json", """
            {
              "schema": 1,
              "content": { "romset": "test" },
              "frameSynced": false,
              "ticks": 3,
              "regions": [{ "name": "ram", "start": "0x100", "size": "1", "file": "ram.bin" }],
              "markers": [{ "tick": 2, "label": "x goes up" }]
            }
            """);
        _recording.Write("ram.bin", [1, 1, 2]);
        var ticks = new byte[3 * 16];
        for (var i = 0; i < 3; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(ticks.AsSpan(i * 16), i * 16_667);
            BinaryPrimitives.WriteInt64LittleEndian(ticks.AsSpan(i * 16 + 8), -1);
        }
        _recording.Write("ticks.bin", ticks);
    }

    public void Dispose() => _recording.Dispose();

    private IReadOnlyList<string> Check(string yaml) =>
        ExpectedValues.Parse(yaml).Check(Recording.Load(_recording.Path), _decoder);

    [Fact]
    public void Passes_when_values_match() =>
        Assert.Empty(Check("""
            at:
              start: { x: 1 }
              1: { x: 0x01 }
              "x goes up": { x: 2 }
            sequences:
              x: [1, 2]
            """));

    [Fact]
    public void Reports_wrong_values_at_ticks_and_markers() =>
        Assert.Equal(
            [
                "at 'start' (tick 0): x is 1, expected 5",
                "at 'x goes up' (tick 2): x is 2, expected 1",
            ],
            Check("""
                at:
                  start: { x: 5 }
                  "x goes up": { x: 1 }
                """));

    [Fact]
    public void Reports_wrong_sequences() =>
        Assert.Equal(["sequences: x is [1, 2], expected [1, 1, 2]"], Check("sequences: { x: [1, 1, 2] }"));

    [Fact]
    public void Reports_unknown_markers_and_variables() =>
        Assert.Equal(
            [
                "at 'no such marker': no such tick or marker",
                "at 'start': the map has no variable 'y'",
                "sequences: the map has no variable 'y'",
            ],
            Check("""
                at:
                  "no such marker": { x: 1 }
                  start: { y: 1 }
                sequences: { y: [1] }
                """));
}
