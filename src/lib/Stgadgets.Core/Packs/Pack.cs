using Stgadgets.Core.Derived;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Packs;

public sealed record ScreenInfo(int Width, int Height, int Rotation);

public sealed record GameInfo(string Title, string? Manufacturer, int? Year, ScreenInfo? Screen);

/// <summary>Everything stgadgets knows about one game: a folder under packs/. See docs/packs.md.</summary>
/// <param name="Id">The MAME parent ROM set, which is also the folder's name.</param>
/// <param name="DirectoryPath">The pack's folder.</param>
/// <param name="Derived">The pack's derived.js, if it has one.</param>
/// <param name="ProviderSettings">The pack's settings for each provider, by provider ID (providers/&lt;id&gt;.yaml).</param>
public sealed record Pack(
    string Id,
    string DirectoryPath,
    GameInfo Game,
    IReadOnlyList<string> Romsets,
    IReadOnlyDictionary<string, uint> ProgramCrcs,
    MemoryMap Map,
    DerivedScript? Derived,
    IReadOnlyDictionary<string, ProviderSettings> ProviderSettings)
{
    // TODO: match on ProgramCrcs first once providers report chip CRCs (docs/memory-maps.md#precedence).
    public bool Matches(ContentInfo content) =>
        content.Romset is not null && Romsets.Contains(content.Romset, StringComparer.OrdinalIgnoreCase);

    /// <summary>The folders under recordings/, each a recording with an optional expected.yaml.</summary>
    public IEnumerable<string> Recordings
    {
        get
        {
            var path = Path.Combine(DirectoryPath, "recordings");
            return Directory.Exists(path) ? Directory.EnumerateDirectories(path).Order() : [];
        }
    }

    /// <summary>The files under tests/derived/, each a set of scenarios for derived.js.</summary>
    public IEnumerable<string> DerivedScenarios
    {
        get
        {
            var path = Path.Combine(DirectoryPath, "tests", "derived");
            return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.yaml").Order() : [];
        }
    }
}
