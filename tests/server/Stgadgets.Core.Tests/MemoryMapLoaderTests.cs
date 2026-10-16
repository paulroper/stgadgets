using Stgadgets.Core.Maps;

namespace Stgadgets.Core.Tests;

public class MemoryMapLoaderTests
{
    private const string Yaml = """
        schema: 1
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
        Assert.Equal(
            [
                new Variable("hiscore", 0x10F000, new(VariableKind.Bcd, 4)),
                new Variable("p1.lives", 4096, new(VariableKind.Unsigned, 1)),
                new Variable("rank", 0x100010, new(VariableKind.Unsigned, 2), "Internal difficulty", Verified: true),
            ],
            map.Variables);
    }

    [Fact]
    public void Reports_bad_short_form()
    {
        var ex = Assert.Throws<FormatException>(() => MemoryMapLoader.Parse(Yaml.Replace("bcd4 @ 0x10F000", "bcd4 0x10F000")));
        Assert.Contains("hiscore", ex.Message);
    }
}
