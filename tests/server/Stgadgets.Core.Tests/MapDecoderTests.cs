using Stgadgets.Core.Maps;
using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Tests;

public class MapDecoderTests
{
    [Theory]
    [InlineData(new byte[] { 0x20, 0x09 }, Endianness.Little, 920L)]   // Space Invaders stores 0920 low byte first
    [InlineData(new byte[] { 0x09, 0x20 }, Endianness.Big, 920L)]
    [InlineData(new byte[] { 0x99 }, Endianness.Little, 99L)]
    [InlineData(new byte[] { 0x1A, 0x00 }, Endianness.Little, null)]   // 0xA isn't a decimal digit
    public void Decodes_bcd(byte[] bytes, Endianness endianness, long? expected) =>
        Assert.Equal(expected, MapDecoder.Decode(new VariableType(VariableKind.Bcd, bytes.Length), bytes, endianness));

    [Fact]
    public void Decodes_integers_in_system_byte_order_unless_overridden()
    {
        byte[] bytes = [0x12, 0x34];
        Assert.Equal(0x3412L, MapDecoder.Decode(VariableType.Parse("u16"), bytes, Endianness.Little));
        Assert.Equal(0x1234L, MapDecoder.Decode(VariableType.Parse("u16"), bytes, Endianness.Big));
        Assert.Equal(0x1234L, MapDecoder.Decode(VariableType.Parse("u16be"), bytes, Endianness.Little));
        Assert.Equal(-1L, MapDecoder.Decode(VariableType.Parse("s8"), [0xFF], Endianness.Little));
    }

    [Fact]
    public void Read_plan_merges_nearby_ranges()
    {
        var plan = ReadPlan.Create([new(0x2100, 1), new(0x20F4, 2), new(0x2400, 4), new(0x20F8, 2)], maxGap: 256);
        Assert.Equal([new AddressRange(0x20F4, 0x0D), new AddressRange(0x2400, 4)], plan.Ranges);
    }
}
