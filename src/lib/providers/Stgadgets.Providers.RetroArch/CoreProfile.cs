using Stgadgets.Core.Memory;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;

namespace Stgadgets.Providers.RetroArch;

/// <summary>
/// How a core exposes the original hardware's RAM through READ_CORE_RAM: which <paramref name="Segments"/> of the
/// original hardware's address space it exposes, and where each one starts in its offsets.
/// <paramref name="WordSwapped"/> means the core keeps each 16-bit word in the host's (little-endian) byte order, as
/// FBNeo does for the 68000, so each pair of bytes comes back swapped. See "core profiles" in docs/providers.md.
/// </summary>
public sealed record CoreProfile(IReadOnlyList<CoreSegment> Segments, bool WordSwapped = false)
{
    /// <summary>A core that exposes <paramref name="ramSize"/> bytes from <paramref name="ramBase"/> as one block.</summary>
    public CoreProfile(uint ramBase, int ramSize, bool wordSwapped = false)
        : this([new CoreSegment(ramBase, 0, ramSize)], wordSwapped)
    {
    }

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

        var profile = (core.RamBase, core.RamSize, core.Segments) switch
        {
            (not null, not null, null) => new CoreProfile(
                PackYaml.ParseNumber(core.RamBase), (int)PackYaml.ParseNumber(core.RamSize), core.WordSwapped),
            (null, null, { Count: > 0 }) => new CoreProfile(core.Segments.Select(ParseSegment).ToList(), core.WordSwapped),
            _ => throw new FormatException($"{settings.Path}: core '{system}' needs either ram_base and ram_size, or segments"),
        };

        // A swapped word split between two reads would come back with its bytes in the wrong reads.
        if (profile.WordSwapped && profile.Segments.Any(s => s.Address % 2 != 0 || s.Offset % 2 != 0 || s.Size % 2 != 0))
            throw new FormatException($"{settings.Path}: core '{system}' swaps words, so its segments must be word-aligned");

        return profile;
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

    /// <summary>
    /// The READ_CORE_RAM reads that make up <paramref name="range"/>: one for each segment it crosses, with where its
    /// bytes start in the range.
    /// </summary>
    public IEnumerable<CoreRead> ToReads(AddressRange range)
    {
        var address = range.Start;
        while (address < range.End)
        {
            var segment = Segments.FirstOrDefault(s => s.Range.Contains(address, 1))
                ?? throw new ArgumentOutOfRangeException(nameof(range), $"{range} is outside the RAM this core exposes");
            var length = (int)(Math.Min(range.End, segment.Range.End) - address);
            yield return new CoreRead((int)(address - range.Start), length, segment.Offset + (address - segment.Address));
            address += (uint)length;
        }
    }

    private static CoreSegment ParseSegment(SegmentDto segment) =>
        segment.Address is null || segment.Offset is null || segment.Size is null
            ? throw new FormatException("each segment needs address, offset and size")
            : new CoreSegment(
                PackYaml.ParseNumber(segment.Address), PackYaml.ParseNumber(segment.Offset), (int)PackYaml.ParseNumber(segment.Size));

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
        public List<SegmentDto>? Segments { get; set; }
        public bool WordSwapped { get; set; }
    }

    private sealed class SegmentDto
    {
        public string? Address { get; set; }
        public string? Offset { get; set; }
        public string? Size { get; set; }
    }
}

/// <summary>
/// <paramref name="Size"/> bytes of the original hardware's RAM from <paramref name="Address"/>, which a core exposes
/// from READ_CORE_RAM offset <paramref name="Offset"/>.
/// </summary>
public sealed record CoreSegment(uint Address, uint Offset, int Size)
{
    public AddressRange Range => new(Address, Size);
}

/// <summary>One READ_CORE_RAM read: <paramref name="Length"/> bytes from core offset <paramref name="Offset"/>, for the
/// bytes from <paramref name="Start"/> in the range being read.</summary>
public readonly record struct CoreRead(int Start, int Length, uint Offset);
