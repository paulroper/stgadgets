using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Maps;

/// <summary>A named value at an address in the original hardware's address space.</summary>
/// <param name="Mask">Picks out some bits: the value is <c>(raw &amp; Mask) &gt;&gt; Shift</c>, or for a bool, whether any are set.</param>
public sealed record Variable(
    string Name, uint Address, VariableType Type, string? Description = null, bool Verified = false, long? Mask = null, int Shift = 0)
{
    public AddressRange Range => new(Address, Type.Size);
}

/// <summary>One game's variables: a pack's map.yaml. See docs/memory-maps.md.</summary>
public sealed record MemoryMap(Endianness Endianness, IReadOnlyList<Variable> Variables);
