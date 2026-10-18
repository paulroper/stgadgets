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
    public void Picks_out_bits_with_mask_and_shift()
    {
        byte[] flags = [0xC5];   // 1100 0101
        Assert.Equal(true, MapDecoder.Decode(new Variable("in_play", 0, VariableType.Parse("bool"), Mask: 0x80), flags, Endianness.Big));
        Assert.Equal(false, MapDecoder.Decode(new Variable("x", 0, VariableType.Parse("bool"), Mask: 0x02), flags, Endianness.Big));
        Assert.Equal(1L, MapDecoder.Decode(new Variable("bits_2_3", 0, VariableType.Parse("u8"), Mask: 0x0C, Shift: 2), flags, Endianness.Big));
        Assert.Equal(0x0AL, MapDecoder.Decode(new Variable("high_nibble", 0, VariableType.Parse("u16"), Mask: 0xF000, Shift: 12), [0xA1, 0x23], Endianness.Big));
        Assert.Equal(0x0FL, MapDecoder.Decode(new Variable("signed", 0, VariableType.Parse("s8"), Mask: 0xF0, Shift: 4), [0xF0], Endianness.Big));   // bits of the raw byte, not of -16
    }

    [Fact]
    public void Read_plan_merges_nearby_ranges()
    {
        var plan = ReadPlan.Create([new(0x2100, 1), new(0x20F4, 2), new(0x2400, 4), new(0x20F8, 2)], maxGap: 256);
        Assert.Equal([new AddressRange(0x20F4, 0x0D), new AddressRange(0x2400, 4)], plan.Ranges);
    }
}
