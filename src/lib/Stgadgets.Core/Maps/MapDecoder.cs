using System.Buffers.Binary;
using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Maps;

/// <summary>Turns snapshots into variable values for one memory map.</summary>
public sealed class MapDecoder(MemoryMap map)
{
    public MemoryMap Map { get; } = map;

    /// <summary>What a provider needs to read for this map.</summary>
    public ReadPlan Plan { get; } = ReadPlan.Create(map.Ranges);

    /// <summary>
    /// Decodes every variable. Values are <see cref="long"/>, <see cref="bool"/>, null if the bytes are invalid (e.g.
    /// bad BCD), or an <see cref="ArrayValue"/> of those for an array.
    /// </summary>
    public Dictionary<string, object?> Decode(Snapshot snapshot)
    {
        var values = new Dictionary<string, object?>(Map.Variables.Count + Map.Arrays.Count);
        foreach (var variable in Map.Variables)
            values[variable.Name] = Decode(variable, snapshot.Read(variable.Address, variable.Type.Size), Map.Endianness);
        foreach (var array in Map.Arrays)
            values[array.Name] = Decode(array, snapshot, Map.Endianness);
        return values;
    }

    /// <summary>
    /// What the map decodes from memory that's all <paramref name="fill"/>: zero for a scenario's starting point, or
    /// varied bytes to warm up derived.js.
    /// </summary>
    public Dictionary<string, object?> DecodeFilled(byte fill)
    {
        var blocks = Plan.Ranges.Select(range => new MemoryBlock(range, Enumerable.Repeat(fill, range.Length).ToArray())).ToList();
        return Decode(new Snapshot(EmulatedFrame: null, TimeSpan.Zero, blocks));
    }

    public static ArrayValue Decode(ArrayVariable array, Snapshot snapshot, Endianness systemEndianness)
    {
        var elements = new IReadOnlyDictionary<string, object?>[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            var entry = array.EntryAddress(i);
            var element = new Dictionary<string, object?>(array.Fields.Count);
            foreach (var field in array.Fields)
                element[field.Name] = Decode(field, snapshot.Read(entry + field.Address, field.Type.Size), systemEndianness);
            elements[i] = element;
        }
        return new ArrayValue(elements);
    }

    /// <summary>Decodes one variable, applying its <see cref="Variable.Mask"/> and <see cref="Variable.Shift"/>.</summary>
    public static object? Decode(Variable variable, ReadOnlySpan<byte> bytes, Endianness systemEndianness)
    {
        if (variable.Mask is not { } mask)
            return Decode(variable.Type, bytes, systemEndianness);

        // Bits are picked out of the unsigned value, whatever the type.
        var raw = (long)Decode(variable.Type with { Kind = VariableKind.Unsigned }, bytes, systemEndianness)!;
        var bits = (raw & mask) >> variable.Shift;
        return variable.Type.Kind == VariableKind.Bool ? bits != 0 : bits;
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
