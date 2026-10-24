using Stgadgets.Core.Derived;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Packs;

/// <summary>Loads a pack folder: pack.yaml, map.yaml, derived.js and providers/*.yaml.</summary>
public static class PackLoader
{
    public static Pack Load(string directory)
    {
        directory = Path.GetFullPath(directory);
        var info = PackYaml.LoadFile(Path.Combine(directory, "pack.yaml"), ParsePackFile);
        if (info.Id != Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)))
            throw new FormatException($"{directory}: pack id '{info.Id}' must match the folder name");

        var map = MemoryMapLoader.LoadFile(Path.Combine(directory, "map.yaml"));
        var derivedPath = Path.Combine(directory, DerivedScript.FileName);
        var derived = File.Exists(derivedPath) ? DerivedScript.Load(derivedPath) : null;
        if (derived?.Values.FirstOrDefault(d => map.Names.Contains(d.Name))?.Name is { } clash)
            throw new FormatException($"{derivedPath}: '{clash}' is already a variable in map.yaml");

        return new Pack(
            info.Id,
            directory,
            info.Game,
            info.Romsets,
            info.ProgramCrcs,
            map,
            derived,
            LoadProviderSettings(Path.Combine(directory, "providers")));
    }

    private sealed record PackFile(string Id, GameInfo Game, List<string> Romsets, Dictionary<string, uint> ProgramCrcs);

    private static PackFile ParsePackFile(string yaml)
    {
        var dto = PackYaml.Deserialize<PackDto>(yaml);
        if (dto.Schema != 1)
            throw new FormatException($"Unsupported schema {dto.Schema}");

        var screen = dto.Screen is { } s ? new ScreenInfo(s.Width, s.Height, s.Rotation) : null;
        return new PackFile(
            Require(dto.Id, "id"),
            new GameInfo(Require(dto.Title, "title"), dto.Manufacturer, dto.Year, screen),
            dto.Match?.Romsets ?? [],
            (dto.Match?.ProgramCrcs ?? []).ToDictionary(kv => kv.Key, kv => PackYaml.ParseNumber(kv.Value)));
    }

    private static Dictionary<string, ProviderSettings> LoadProviderSettings(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.yaml").ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => new ProviderSettings(path, File.ReadAllText(path)))
            : [];

    private static string Require(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new FormatException($"Missing '{field}'") : value;

    // YAML shape.
    private sealed class PackDto
    {
        public int Schema { get; set; }
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Manufacturer { get; set; }
        public int? Year { get; set; }
        public ScreenDto? Screen { get; set; }
        public MatchDto? Match { get; set; }
    }

    private sealed class ScreenDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Rotation { get; set; }
    }

    private sealed class MatchDto
    {
        public List<string>? Romsets { get; set; }
        public Dictionary<string, string>? ProgramCrcs { get; set; }
    }
}
