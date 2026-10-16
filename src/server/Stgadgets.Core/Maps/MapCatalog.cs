using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Maps;

/// <summary>All known memory maps.</summary>
public sealed class MapCatalog(IReadOnlyList<MemoryMap> maps)
{
    public IReadOnlyList<MemoryMap> Maps { get; } = maps;

    public static MapCatalog LoadDirectory(string path) =>
        new(Directory.EnumerateFiles(path, "*.yaml").Order().Select(MemoryMapLoader.LoadFile).ToList());

    public MemoryMap? Find(ContentInfo content) => Maps.FirstOrDefault(m => m.Matches(content));
}
