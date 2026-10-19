using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Packs;

/// <summary>All installed packs.</summary>
public sealed class PackCatalog(IReadOnlyList<Pack> packs)
{
    public IReadOnlyList<Pack> Packs { get; } = packs;

    /// <summary>Loads every folder under <paramref name="path"/> that has a pack.yaml.</summary>
    public static PackCatalog LoadDirectory(string path) =>
        new(Directory.EnumerateDirectories(path)
            .Where(dir => File.Exists(Path.Combine(dir, "pack.yaml")))
            .Order()
            .Select(PackLoader.Load)
            .ToList());

    public Pack? Find(ContentInfo content) => Packs.FirstOrDefault(p => p.Matches(content));
}
