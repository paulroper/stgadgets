using Stgadgets.Core.Memory;
using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Maps;

public sealed record ScreenInfo(int Width, int Height, int Rotation);

public sealed record GameInfo(string Id, string Title, string? Manufacturer, int? Year, ScreenInfo? Screen);

/// <summary>A named value at an address in the original hardware's address space.</summary>
public sealed record Variable(string Name, uint Address, VariableType Type, string? Description = null, bool Verified = false)
{
    public AddressRange Range => new(Address, Type.Size);
}

/// <summary>Matches one game to its variables. See docs/memory-maps.md.</summary>
public sealed record MemoryMap(
    GameInfo Game,
    IReadOnlyList<string> Romsets,
    IReadOnlyDictionary<string, uint> ProgramCrcs,
    Endianness Endianness,
    IReadOnlyList<Variable> Variables)
{
    // TODO: match on ProgramCrcs once providers report chip CRCs (docs/memory-maps.md#precedence).
    public bool Matches(ContentInfo content) =>
        content.Romset is not null && Romsets.Contains(content.Romset, StringComparer.OrdinalIgnoreCase);
}
