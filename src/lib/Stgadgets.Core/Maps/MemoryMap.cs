using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Maps;

/// <summary>A named value at an address in the original hardware's address space.</summary>
/// <param name="Mask">Picks out some bits: the value is <c>(raw &amp; Mask) &gt;&gt; Shift</c>, or for a bool, whether any are set.</param>
public sealed record Variable(
    string Name, uint Address, VariableType Type, string? Description = null, bool Verified = false, long? Mask = null, int Shift = 0)
{
    public AddressRange Range => new(Address, Type.Size);
}

/// <summary>
/// A table of <paramref name="Count"/> entries from <paramref name="Address"/>, <paramref name="Stride"/> bytes apart,
/// such as the enemies on screen. Each <see cref="Variable"/> in <paramref name="Fields"/> is one field of an entry, and
/// its <see cref="Variable.Address"/> is the offset from the entry's start.
/// </summary>
public sealed record ArrayVariable(
    string Name, uint Address, int Count, int Stride, IReadOnlyList<Variable> Fields, string? Description = null, bool Verified = false)
{
    /// <summary>What to read for each entry: from its first field to the end of its last, not the whole stride.</summary>
    public IEnumerable<AddressRange> Ranges
    {
        get
        {
            var (start, end) = (Fields.Min(f => f.Address), Fields.Max(f => f.Range.End));
            return Enumerable.Range(0, Count).Select(i => new AddressRange(EntryAddress(i) + start, (int)(end - start)));
        }
    }

    public uint EntryAddress(int index) => Address + (uint)(index * Stride);
}

/// <summary>One game's variables: a pack's map.yaml. See docs/memory-maps.md.</summary>
public sealed record MemoryMap(Endianness Endianness, IReadOnlyList<Variable> Variables, IReadOnlyList<ArrayVariable> Arrays)
{
    public MemoryMap(Endianness endianness, IReadOnlyList<Variable> variables)
        : this(endianness, variables, [])
    {
    }

    /// <summary>Every variable's name, single values and arrays.</summary>
    public IEnumerable<string> Names => Variables.Select(v => v.Name).Concat(Arrays.Select(a => a.Name));

    /// <summary>Everything a provider must read for the map.</summary>
    public IEnumerable<AddressRange> Ranges => Variables.Select(v => v.Range).Concat(Arrays.SelectMany(a => a.Ranges));
}
