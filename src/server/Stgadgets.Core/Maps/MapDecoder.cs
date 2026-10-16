using System.Buffers.Binary;
using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Maps;

/// <summary>Turns snapshots into variable values for one memory map.</summary>
public sealed class MapDecoder(MemoryMap map)
{
    public MemoryMap Map { get; } = map;

    /// <summary>What a provider needs to read for this map.</summary>
    public ReadPlan Plan { get; } = ReadPlan.Create(map.Variables.Select(v => v.Range));

    /// <summary>Decodes every variable. Values are <see cref="long"/>, <see cref="bool"/>, or null if the bytes are invalid (e.g. bad BCD).</summary>
    public Dictionary<string, object?> Decode(Snapshot snapshot)
    {
        var values = new Dictionary<string, object?>(Map.Variables.Count);
        foreach (var variable in Map.Variables)
            values[variable.Name] = Decode(variable.Type, snapshot.Read(variable.Address, variable.Type.Size), Map.Endianness);
        return values;
    }

    public static object? Decode(VariableType type, ReadOnlySpan<byte> bytes, Endianness systemEndianness)
    {
        var little = (type.Endianness ?? systemEndianness) == Endianness.Little;
        return (type.Kind, type.Size) switch
        {
            (VariableKind.Bool, _) => bytes[0] != 0,
            (VariableKind.Unsigned, 1) => (long)bytes[0],
            (VariableKind.Unsigned, 2) => (long)(little ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes)),
            (VariableKind.Unsigned, 4) => (long)(little ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes)),
            (VariableKind.Signed, 1) => (long)(sbyte)bytes[0],
            (VariableKind.Signed, 2) => (long)(little ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : BinaryPrimitives.ReadInt16BigEndian(bytes)),
            (VariableKind.Signed, 4) => (long)(little ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes)),
            (VariableKind.Bcd, _) => DecodeBcd(bytes, little),
            _ => throw new NotSupportedException($"Can't decode {type}"),
        };
    }

    private static long? DecodeBcd(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        long value = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[littleEndian ? bytes.Length - 1 - i : i];   // most significant byte first
            int high = b >> 4, low = b & 0x0F;
            if (high > 9 || low > 9)
                return null;
            value = value * 100 + high * 10 + low;
        }
        return value;
    }
}
