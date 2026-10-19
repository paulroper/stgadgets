using Stgadgets.Core.Maps;

namespace Stgadgets.Core.Tests;

public class VariableTypeTests
{
    [Theory]
    [InlineData("u8", VariableKind.Unsigned, 1, null)]
    [InlineData("u16", VariableKind.Unsigned, 2, null)]
    [InlineData("s32", VariableKind.Signed, 4, null)]
    [InlineData("u16be", VariableKind.Unsigned, 2, Endianness.Big)]
    [InlineData("s16le", VariableKind.Signed, 2, Endianness.Little)]
    [InlineData("bcd2", VariableKind.Bcd, 2, null)]
    [InlineData("bool", VariableKind.Bool, 1, null)]
    public void Parses_and_prints_type_names(string text, VariableKind kind, int size, Endianness? endianness)
    {
        var type = VariableType.Parse(text);
        Assert.Equal(new VariableType(kind, size, endianness), type);
        Assert.Equal(text, type.ToString());
    }

    [Theory]
    [InlineData("u24")]
    [InlineData("bcd0")]
    [InlineData("bcd2be")]
    [InlineData("float")]
    public void Rejects_unknown_types(string text) =>
        Assert.Throws<FormatException>(() => VariableType.Parse(text));
}
