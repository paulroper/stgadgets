using System.Buffers.Binary;
using System.CommandLine;

namespace Stgadgets.Cli.Tests.Recordings;

/// <summary>
/// A pack whose map has one byte at 0x100 and whose derived.js doubles it, with a recording of two bytes from 0x100:
/// 0x100 goes 1, 1, 2, 2 and 0x101 goes 5, 4, 4, 9. Tick timestamps are 1000, 2000, … microseconds.
/// </summary>
public sealed class RecordingFixture : IDisposable
{
    private readonly TempDirectory _packs = new();

    public RecordingFixture()
    {
        _packs.Write("test/pack.yaml", "schema: 1\nid: test\ntitle: Test Game\nmatch: { romsets: [test] }");
        _packs.Write("test/map.yaml", """
            schema: 1
            system: { endianness: little }
            variables:
              x: u8 @ 0x100
            """);
        _packs.Write("test/derived.js", "export default { 'x.doubled': v => v.x * 2 };");
        _packs.Write("test/recordings/run/manifest.json", """
            {
              "schema": 1,
              "content": { "romset": "test" },
              "capturedAt": "2026-10-19T20:00:00Z",
              "frameSynced": false,
              "ticks": 4,
              "regions": [{ "name": "ram", "start": "0x100", "size": "2", "file": "ram.bin" }],
              "markers": [{ "tick": 2, "label": "x goes up" }],
              "notes": "Made up."
            }
            """);
        _packs.Write("test/recordings/run/ram.bin", [1, 5, 1, 4, 2, 4, 2, 9]);
        var ticks = new byte[4 * 16];
        for (var i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(ticks.AsSpan(i * 16), (i + 1) * 1000);
            BinaryPrimitives.WriteInt64LittleEndian(ticks.AsSpan(i * 16 + 8), -1);
        }
        _packs.Write("test/recordings/run/ticks.bin", ticks);
    }

    public string Packs => _packs.Path;

    public string Recording => Path.Combine(_packs.Path, "test", "recordings", "run");

    public void Dispose() => _packs.Dispose();

    /// <summary>Runs <c>stgadgets</c> with <paramref name="args"/>, capturing its output.</summary>
    public static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = StgadgetsCommand.Create().Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exitCode, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }
}
