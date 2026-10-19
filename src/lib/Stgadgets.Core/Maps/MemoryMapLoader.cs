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
                if (longForm.Keys.Select(k => k.ToString()).FirstOrDefault(k => !LongFormKeys.Contains(k!)) is { } unknown)
                    throw new FormatException($"Variable '{name}' has an unknown option '{unknown}'; expected {string.Join(", ", LongFormKeys)}");

                var type = VariableType.Parse(Get("type") ?? throw new FormatException($"Variable '{name}' has no type"));
                long? mask = Get("mask") is { } m ? PackYaml.ParseNumber(m) : null;
                var shift = Get("shift") is { } s ? (int)PackYaml.ParseNumber(s) : 0;
                if (mask is null && shift != 0)
                    throw new FormatException($"Variable '{name}': shift needs a mask");
                if (mask is not null && type.Kind == VariableKind.Bcd)
                    throw new FormatException($"Variable '{name}': mask and shift don't apply to BCD");
                if (shift is < 0 or > 31)
                    throw new FormatException($"Variable '{name}': shift must be 0-31");

                return new Variable(
                    name,
                    PackYaml.ParseNumber(Get("address") ?? throw new FormatException($"Variable '{name}' has no address")),
                    type,
                    Get("description"),
                    Get("verified") is { } verified && bool.Parse(verified),
                    mask,
                    shift);

            default:
                throw new FormatException($"Variable '{name}' must be '<type> @ <address>' or a mapping");
        }
    }

    private static readonly string[] LongFormKeys = ["type", "address", "description", "verified", "mask", "shift"];

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
