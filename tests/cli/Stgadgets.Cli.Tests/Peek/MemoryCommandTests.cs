using System.CommandLine;
using Stgadgets.Cli.Peek;
using Stgadgets.Providers;

namespace Stgadgets.Cli.Tests.Peek;

/// <summary><c>stgadgets peek memory</c>, against a fake emulator running a test game with memory 1, 7, 3, 4 from 0x100.</summary>
public sealed class MemoryCommandTests : IDisposable
{
    private readonly TempDirectory _packs = new();
    private readonly FakeEmulator _emulator = new() { Memory = [[1, 7, 3, 4], [2, 7, 3, 4]] };

    public MemoryCommandTests()
    {
        _packs.Write("test/pack.yaml", "schema: 1\nid: test\ntitle: Test Game\nmatch: { romsets: [test] }");
        _packs.Write("test/map.yaml", """
            schema: 1
            system: { endianness: little }
            variables:
              p1.score: u8 @ 0x100
              p1.lives: u8 @ 0x101
              enemies:
                address: 0x102
                count: 2
                stride: 1
                fields:
                  hp: u8 @ 0x00
            """);
        _packs.Write("test/providers/fake.yaml", "ready: true");
    }

    public void Dispose() => _packs.Dispose();

    private (int ExitCode, string Output, string Error) Peek(params string[] args)
    {
        var providers = new Dictionary<string, ConnectProvider> { ["fake"] = (endpoint, _) => _emulator.Connect(endpoint) };
        var root = new RootCommand { PeekCommand.Create(providers) };
        var (output, error) = (new StringWriter(), new StringWriter());
        var exitCode = root.Parse(["peek", "memory", "--provider", "fake", "--packs", _packs.Path, .. args])
            .Invoke(new InvocationConfiguration { Output = output, Error = error });
        return (exitCode, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Reads_every_variable_from_the_first_snapshot() =>
        Assert.Equal((0, "p1.score  1\np1.lives  7\n", ""), Peek());

    [Fact]
    public void Reads_the_variables_and_array_fields_asked_for() =>
        Assert.Equal((0, "enemies[1].hp  4\np1.lives       7\n", ""), Peek("enemies[1].hp", "p1.lives"));

    [Fact]
    public void Reads_the_variables_a_pattern_matches_with_arrays_one_field_a_line() =>
        Assert.Equal((0, "enemies[0].hp  3\nenemies[1].hp  4\n", ""), Peek("enem*"));

    [Fact]
    public void Connects_to_the_endpoint_given()
    {
        Peek("--endpoint", "somewhere:1234");

        Assert.Equal("somewhere:1234", _emulator.Endpoint);
    }

    [Fact]
    public void Doesnt_read_recordings()
    {
        var errors = new RootCommand { PeekCommand.Create() }.Parse(["peek", "memory", "--provider", "replay"]).Errors;

        Assert.Contains(errors, e => e.Message.Contains("'replay' not recognized", StringComparison.Ordinal));
    }

    [Fact]
    public void Says_when_the_map_has_no_such_variable() =>
        Assert.Equal((1, "", "test's map has no 'p2.score'\n"), Peek("p2.score"));

    [Fact]
    public void Says_when_a_pattern_matches_nothing() =>
        Assert.Equal((1, "", "Nothing in test's map matches 'p2.*'\n"), Peek("p2.*"));

    [Fact]
    public void Says_when_no_pack_matches_the_game()
    {
        File.WriteAllText(Path.Combine(_packs.Path, "test", "pack.yaml"), "schema: 1\nid: test\ntitle: Test Game\nmatch: { romsets: [other] }");

        Assert.Equal((1, "", $"No pack in {_packs.Path} matches 'test'\n"), Peek());
    }

    [Fact]
    public void Says_when_the_provider_cant_read_the_game()
    {
        File.Delete(Path.Combine(_packs.Path, "test", "providers", "fake.yaml"));

        Assert.StartsWith("fake: no settings for fake_core. Add the settings to the pack's providers/fake.yaml", Peek().Error);
    }

    [Fact]
    public void Says_when_the_emulator_sends_nothing()
    {
        _emulator.Memory = [];

        Assert.Equal((1, "", "fake sent nothing: the game stopped or changed\n"), Peek());
    }
}
