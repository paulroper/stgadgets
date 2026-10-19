using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;

namespace Stgadgets.Providers.RetroArch;

/// <summary>
/// How a core exposes the original hardware's RAM through READ_CORE_RAM: offset 0 is <paramref name="RamBase"/>
/// in the original hardware's address space. <paramref name="WordSwapped"/> means the core keeps each 16-bit word in
/// the host's (little-endian) byte order, as FBNeo does for the 68000, so each pair of bytes comes back swapped.
/// See "core profiles" in docs/providers.md.
/// </summary>
public sealed record CoreProfile(uint RamBase, int RamSize, bool WordSwapped = false)
{
    /// <summary>The profile for <paramref name="system"/> in a pack's providers/retroarch.yaml, if it has one.</summary>
    public static CoreProfile? Find(ProviderSettings? settings, string? system)
    {
        if (settings is null || system is null)
            return null;

        var file = settings.Read<SettingsDto>();
        if (file.Schema != 1)
            throw new FormatException($"{settings.Path}: unsupported schema {file.Schema}");
        if (!file.Cores.TryGetValue(system, out var core))
            return null;
        if (core.RamBase is null || core.RamSize is null)
            throw new FormatException($"{settings.Path}: core '{system}' needs ram_base and ram_size");

        return new CoreProfile(PackYaml.ParseNumber(core.RamBase), (int)PackYaml.ParseNumber(core.RamSize), core.WordSwapped);
    }

    /// <summary>The range to read for <paramref name="range"/>: widened to whole words when the core swaps them.</summary>
    public AddressRange ToReadRange(AddressRange range)
    {
        if (!WordSwapped)
            return range;
        var start = range.Start & ~1u;
        return new AddressRange(start, (int)(((range.End + 1) & ~1u) - start));
    }

    /// <summary>Puts bytes read from <see cref="ToReadRange"/> back in the original hardware's order.</summary>
    public void Unswap(Span<byte> bytes)
    {
        if (!WordSwapped)
            return;
        for (var i = 0; i + 1 < bytes.Length; i += 2)
            (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]);
    }

    /// <summary>Converts an address range to a READ_CORE_RAM offset.</summary>
    public uint ToOffset(AddressRange range) =>
        range.Start >= RamBase && range.End <= RamBase + (uint)RamSize
            ? range.Start - RamBase
            : throw new ArgumentOutOfRangeException(nameof(range), $"{range} is outside the RAM this core exposes");

    // YAML shape of providers/retroarch.yaml.
    private sealed class SettingsDto
    {
        public int Schema { get; set; }
        public Dictionary<string, CoreDto> Cores { get; set; } = [];
    }

    private sealed class CoreDto
    {
        public string? RamBase { get; set; }
        public string? RamSize { get; set; }
        public bool WordSwapped { get; set; }
    }
}
