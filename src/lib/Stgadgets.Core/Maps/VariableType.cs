using System.Text.RegularExpressions;

namespace Stgadgets.Core.Maps;

public enum Endianness { Little, Big }

public enum VariableKind { Unsigned, Signed, Bool, Bcd }

/// <summary>How a variable's bytes are decoded. See "Types" in docs/memory-maps.md.</summary>
/// <param name="Endianness">Overrides the system's byte order when set.</param>
public sealed partial record VariableType(VariableKind Kind, int Size, Endianness? Endianness = null)
{
    /// <summary>Parses a type name such as <c>u8</c>, <c>s16be</c>, <c>bool</c> or <c>bcd2</c>.</summary>
    public static VariableType Parse(string text)
    {
        if (text == "bool")
            return new(VariableKind.Bool, 1);

        var match = TypePattern().Match(text);
        if (!match.Success)
            throw new FormatException($"Unknown variable type '{text}'");

        var size = int.Parse(match.Groups["size"].Value);
        Endianness? endianness = match.Groups["endian"].Value switch
        {
            "le" => Maps.Endianness.Little,
            "be" => Maps.Endianness.Big,
            _ => null,
        };

        return match.Groups["kind"].Value switch
        {
            "u" when size is 8 or 16 or 32 => new(VariableKind.Unsigned, size / 8, endianness),
            "s" when size is 8 or 16 or 32 => new(VariableKind.Signed, size / 8, endianness),
            "bcd" when size is >= 1 and <= 8 && endianness is null => new(VariableKind.Bcd, size),
            _ => throw new FormatException($"Unknown variable type '{text}'"),
        };
    }

    /// <summary>The type's name as written in maps, e.g. <c>u16be</c>.</summary>
    public override string ToString() => Kind switch
    {
        VariableKind.Bool => "bool",
        VariableKind.Bcd => $"bcd{Size}",
        _ => $"{(Kind == VariableKind.Unsigned ? "u" : "s")}{Size * 8}{Endianness switch
        {
            Maps.Endianness.Little => "le",
            Maps.Endianness.Big => "be",
            _ => "",
        }}",
    };

    [GeneratedRegex(@"^(?<kind>u|s|bcd)(?<size>\d+)(?<endian>le|be)?$")]
    private static partial Regex TypePattern();
}
