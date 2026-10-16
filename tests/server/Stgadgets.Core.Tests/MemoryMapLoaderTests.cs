using Stgadgets.Core.Maps;
using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Tests;

public class MemoryMapLoaderTests
{
    private const string Yaml = """
        schema: 1
        game: { id: test, title: Test Game }
        match: { romsets: [test, testj], program_crcs: { prg.bin: 0x1234abcd } }
        system: { cpu: m68000, endianness: big }
        variables:
          hiscore: bcd4 @ 0x10F000
          p1.lives: u8 @ 4096
          rank:
            type: u16
            address: 0x100010
            description: Internal difficulty
            verified: true
        """;

    [Fact]
    public void Parses_short_and_long_form_variables()
    {
        var map = MemoryMapLoader.Parse(Yaml);

        Assert.Equal(Endianness.Big, map.Endianness);
        Assert.Equal(0x1234abcdu, map.ProgramCrcs["prg.bin"]);
        Assert.Equal(
            [
                new Variable("hiscore", 0x10F000, new(VariableKind.Bcd, 4)),
                new Variable("p1.lives", 4096, new(VariableKind.Unsigned, 1)),
                new Variable("rank", 0x100010, new(VariableKind.Unsigned, 2), "Internal difficulty", Verified: true),
            ],
            map.Variables);
    }

    [Fact]
    public void Matches_romset_names_case_insensitively()
    {
        var map = MemoryMapLoader.Parse(Yaml);
        Assert.True(map.Matches(new ContentInfo("TESTJ", "fb_alpha")));
        Assert.False(map.Matches(new ContentInfo("other", "fb_alpha")));
    }

    [Fact]
    public void Reports_bad_short_form()
    {
        var ex = Assert.Throws<FormatException>(() => MemoryMapLoader.Parse(Yaml.Replace("bcd4 @ 0x10F000", "bcd4 0x10F000")));
        Assert.Contains("hiscore", ex.Message);
    }

    [Fact]
    public void Loads_every_map_in_the_repo() =>
        Assert.NotEmpty(MapCatalog.LoadDirectory(RepoPaths.Maps).Maps);
}
