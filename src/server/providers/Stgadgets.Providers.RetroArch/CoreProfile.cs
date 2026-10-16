using Stgadgets.Core.Memory;

namespace Stgadgets.Providers.RetroArch;

/// <summary>
/// How a core exposes the original hardware's RAM through READ_CORE_RAM: offset 0 is <paramref name="RamBase"/>
/// in the original hardware's address space. See "core profile" in docs/providers.md.
/// </summary>
public sealed record CoreProfile(uint RamBase, int RamSize)
{
    // Found by hand during the spike. Each new core/game pairing needs checking (docs/memory-maps.md#finding-addresses).
    private static readonly Dictionary<(string System, string Romset), CoreProfile> Known = new()
    {
        [("fb_alpha", "invaders")] = new(0x2000, 0x2000),
    };

    public static CoreProfile? Find(string? system, string? romset) =>
        system is not null && romset is not null && Known.TryGetValue((system, romset), out var profile) ? profile : null;

    /// <summary>Converts an address range to a READ_CORE_RAM offset.</summary>
    public uint ToOffset(AddressRange range) =>
        range.Start >= RamBase && range.End <= RamBase + (uint)RamSize
            ? range.Start - RamBase
            : throw new ArgumentOutOfRangeException(nameof(range), $"{range} is outside the RAM this core exposes");
}
