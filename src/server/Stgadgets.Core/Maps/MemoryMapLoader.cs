using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Stgadgets.Core.Maps;

/// <summary>Loads memory map YAML files (maps/*.yaml).</summary>
public static partial class MemoryMapLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static MemoryMap LoadFile(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is FormatException or YamlDotNet.Core.YamlException)
        {
            throw new FormatException($"{path}: {ex.Message}", ex);
        }
    }

    public static MemoryMap Parse(string yaml)
    {
        var dto = Deserializer.Deserialize<MapDto>(yaml) ?? throw new FormatException("Empty map");
        if (dto.Schema != 1)
            throw new FormatException($"Unsupported schema {dto.Schema}");

        var game = dto.Game ?? throw new FormatException("Missing 'game'");
        var screen = game.Screen is { } s ? new ScreenInfo(s.Width, s.Height, s.Rotation) : null;

        return new MemoryMap(
            new GameInfo(Require(game.Id, "game.id"), Require(game.Title, "game.title"), game.Manufacturer, game.Year, screen),
            dto.Match?.Romsets ?? [],
            (dto.Match?.ProgramCrcs ?? []).ToDictionary(kv => kv.Key, kv => ParseNumber(kv.Value)),
            dto.System?.Endianness switch
            {
                "little" => Endianness.Little,
                "big" => Endianness.Big,
                var other => throw new FormatException($"system.endianness must be 'little' or 'big', not '{other}'"),
            },
            dto.Variables.Select(kv => ParseVariable(kv.Key, kv.Value)).ToList());
    }

    private static Variable ParseVariable(string name, object value)
    {
        switch (value)
        {
            // Short form: "<type> @ <address>"
            case string shortForm:
                var match = ShortForm().Match(shortForm);
                if (!match.Success)
                    throw new FormatException($"Variable '{name}': expected '<type> @ <address>', got '{shortForm}'");
                return new Variable(name, ParseNumber(match.Groups["address"].Value), VariableType.Parse(match.Groups["type"].Value));

            // Long form: a mapping with type, address and options
            case Dictionary<object, object> longForm:
                string? Get(string key) => longForm.TryGetValue(key, out var v) ? v?.ToString() : null;
                return new Variable(
                    name,
                    ParseNumber(Get("address") ?? throw new FormatException($"Variable '{name}' has no address")),
                    VariableType.Parse(Get("type") ?? throw new FormatException($"Variable '{name}' has no type")),
                    Get("description"),
                    Get("verified") is { } verified && bool.Parse(verified));

            default:
                throw new FormatException($"Variable '{name}' must be '<type> @ <address>' or a mapping");
        }
    }

    internal static uint ParseNumber(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.Parse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : uint.Parse(text, CultureInfo.InvariantCulture);

    private static string Require(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new FormatException($"Missing '{field}'") : value;

    [GeneratedRegex(@"^\s*(?<type>\w+)\s*@\s*(?<address>0x[0-9A-Fa-f]+|\d+)\s*$")]
    private static partial Regex ShortForm();

    // YAML shape. Numbers are read as strings so hex like 0x20F8 parses the same everywhere.
    private sealed class MapDto
    {
        public int Schema { get; set; }
        public GameDto? Game { get; set; }
        public MatchDto? Match { get; set; }
        public SystemDto? System { get; set; }
        public Dictionary<string, object> Variables { get; set; } = [];
    }

    private sealed class GameDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Manufacturer { get; set; }
        public int? Year { get; set; }
        public ScreenDto? Screen { get; set; }
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

    private sealed class SystemDto
    {
        public string? Cpu { get; set; }
        public string? Endianness { get; set; }
    }
}
