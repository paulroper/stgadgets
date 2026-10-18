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
    public void Parses_mask_and_shift()
    {
        var map = MemoryMapLoader.Parse("""
            schema: 1
            system: { cpu: m68000, endianness: big }
            variables:
              in_play: { type: bool, address: 0x102C8C, mask: 0x80 }
              extends: { type: u8, address: 0x101299, mask: 0x0C, shift: 2 }
            """);

        Assert.Equal(
            [
                new Variable("in_play", 0x102C8C, new(VariableKind.Bool, 1), Mask: 0x80),
                new Variable("extends", 0x101299, new(VariableKind.Unsigned, 1), Mask: 0x0C, Shift: 2),
            ],
            map.Variables);
    }

    [Theory]
    [InlineData("scale: 10", "unknown option 'scale'")]
    [InlineData("shift: 2", "shift needs a mask")]
    [InlineData("mask: 0x80\n    shift: 40", "shift must be 0-31")]
    public void Reports_bad_long_form_options(string options, string error)
    {
        var yaml = Yaml.Replace("    verified: true", "    verified: true\n    " + options);
        var ex = Assert.Throws<FormatException>(() => MemoryMapLoader.Parse(yaml));
        Assert.Contains("rank", ex.Message);
        Assert.Contains(error, ex.Message);
    }

    [Fact]
    public void Reports_mask_on_bcd()
    {
        var ex = Assert.Throws<FormatException>(() => MemoryMapLoader.Parse(Yaml.Replace("hiscore: bcd4 @ 0x10F000", "hiscore: { type: bcd4, address: 0x10F000, mask: 0xFF }")));
        Assert.Contains("don't apply to BCD", ex.Message);
    }

    [Fact]
    public void Reports_bad_short_form()
    {
        var ex = Assert.Throws<FormatException>(() => MemoryMapLoader.Parse(Yaml.Replace("bcd4 @ 0x10F000", "bcd4 0x10F000")));
        Assert.Contains("hiscore", ex.Message);
    }
}
