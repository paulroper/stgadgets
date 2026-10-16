using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;

namespace Stgadgets.Providers.RetroArch;

/// <summary>
/// How a core exposes the original hardware's RAM through READ_CORE_RAM: offset 0 is <paramref name="RamBase"/>
/// in the original hardware's address space. See "core profiles" in docs/providers.md.
/// </summary>
public sealed record CoreProfile(uint RamBase, int RamSize)
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

        return new CoreProfile(PackYaml.ParseNumber(core.RamBase), (int)PackYaml.ParseNumber(core.RamSize));
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
    }
}
