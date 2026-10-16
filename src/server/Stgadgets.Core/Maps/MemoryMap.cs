using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Maps;

/// <summary>A named value at an address in the original hardware's address space.</summary>
public sealed record Variable(string Name, uint Address, VariableType Type, string? Description = null, bool Verified = false)
{
    public AddressRange Range => new(Address, Type.Size);
}

/// <summary>One game's variables: a pack's map.yaml. See docs/memory-maps.md.</summary>
public sealed record MemoryMap(Endianness Endianness, IReadOnlyList<Variable> Variables);
