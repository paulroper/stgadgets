using System.Text.RegularExpressions;
using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Maps;

/// <summary>Loads memory maps (a pack's map.yaml).</summary>
public static partial class MemoryMapLoader
{
    public static MemoryMap LoadFile(string path) => PackYaml.LoadFile(path, Parse);

    public static MemoryMap Parse(string yaml)
    {
        var dto = PackYaml.Deserialize<MapDto>(yaml);
        if (dto.Schema != 1)
            throw new FormatException($"Unsupported schema {dto.Schema}");

        return new MemoryMap(
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
                return new Variable(name, PackYaml.ParseNumber(match.Groups["address"].Value), VariableType.Parse(match.Groups["type"].Value));

            // Long form: a mapping with type, address and options
            case Dictionary<object, object> longForm:
                string? Get(string key) => longForm.TryGetValue(key, out var v) ? v?.ToString() : null;
                return new Variable(
                    name,
                    PackYaml.ParseNumber(Get("address") ?? throw new FormatException($"Variable '{name}' has no address")),
                    VariableType.Parse(Get("type") ?? throw new FormatException($"Variable '{name}' has no type")),
                    Get("description"),
                    Get("verified") is { } verified && bool.Parse(verified));

            default:
                throw new FormatException($"Variable '{name}' must be '<type> @ <address>' or a mapping");
        }
    }

    [GeneratedRegex(@"^\s*(?<type>\w+)\s*@\s*(?<address>0x[0-9A-Fa-f]+|\d+)\s*$")]
    private static partial Regex ShortForm();

    // YAML shape.
    private sealed class MapDto
    {
        public int Schema { get; set; }
        public SystemDto? System { get; set; }
        public Dictionary<string, object> Variables { get; set; } = [];
    }

    private sealed class SystemDto
    {
        public string? Cpu { get; set; }
        public string? Endianness { get; set; }
    }
}
