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

        var endianness = dto.System?.Endianness switch
        {
            "little" => Endianness.Little,
            "big" => Endianness.Big,
            var other => throw new FormatException($"system.endianness must be 'little' or 'big', not '{other}'"),
        };
        var arrays = dto.Variables.Where(kv => IsArray(kv.Value)).ToList();
        return new MemoryMap(
            endianness,
            dto.Variables.Except(arrays).Select(kv => ParseVariable($"Variable '{kv.Key}'", kv.Key, kv.Value, "address")).ToList(),
            arrays.Select(kv => ParseArray(kv.Key, (Dictionary<object, object>)kv.Value)).ToList());
    }

    private static bool IsArray(object value) => value is Dictionary<object, object> mapping && mapping.ContainsKey("fields");

    private static ArrayVariable ParseArray(string name, Dictionary<object, object> mapping)
    {
        string? Get(string key) => mapping.TryGetValue(key, out var v) ? v?.ToString() : null;
        if (mapping.Keys.Select(k => k.ToString()).FirstOrDefault(k => !ArrayKeys.Contains(k!)) is { } unknown)
            throw new FormatException($"Array '{name}' has an unknown option '{unknown}'; expected {string.Join(", ", ArrayKeys)}");
        if (mapping["fields"] is not Dictionary<object, object> { Count: > 0 } fieldsDto)
            throw new FormatException($"Array '{name}': fields must map each field's name to '<type> @ <offset>' or a mapping");

        var count = (int)PackYaml.ParseNumber(Get("count") ?? throw new FormatException($"Array '{name}' has no count"));
        var stride = (int)PackYaml.ParseNumber(Get("stride") ?? throw new FormatException($"Array '{name}' has no stride"));
        if (count < 1 || stride < 1)
            throw new FormatException($"Array '{name}': count and stride must be at least 1");

        var fields = fieldsDto
            .Select(kv => ParseVariable($"Array '{name}' field '{kv.Key}'", kv.Key.ToString()!, kv.Value, "offset"))
            .ToList();
        if (fields.FirstOrDefault(f => f.Range.End > stride) is { } outside)
            throw new FormatException($"Array '{name}': field '{outside.Name}' runs past the entry's {stride} bytes (stride)");

        return new ArrayVariable(
            name,
            PackYaml.ParseNumber(Get("address") ?? throw new FormatException($"Array '{name}' has no address")),
            count,
            stride,
            fields,
            Get("description"),
            Get("verified") is { } verified && bool.Parse(verified));
    }

    /// <summary>A variable, or an array's field, whose <paramref name="addressKey"/> is "address" or "offset".</summary>
    private static Variable ParseVariable(string what, string name, object value, string addressKey)
    {
        switch (value)
        {
            // Short form: "<type> @ <address or offset>"
            case string shortForm:
                var match = ShortForm().Match(shortForm);
                if (!match.Success)
                    throw new FormatException($"{what}: expected '<type> @ <{addressKey}>', got '{shortForm}'");
                return new Variable(name, PackYaml.ParseNumber(match.Groups["address"].Value), VariableType.Parse(match.Groups["type"].Value));

            // Long form: a mapping with type, address or offset, and options
            case Dictionary<object, object> longForm:
                string? Get(string key) => longForm.TryGetValue(key, out var v) ? v?.ToString() : null;
                var keys = LongFormKeys.Prepend(addressKey).ToArray();
                if (longForm.Keys.Select(k => k.ToString()).FirstOrDefault(k => !keys.Contains(k!)) is { } unknown)
                    throw new FormatException($"{what} has an unknown option '{unknown}'; expected {string.Join(", ", keys)}");

                var type = VariableType.Parse(Get("type") ?? throw new FormatException($"{what} has no type"));
                long? mask = Get("mask") is { } m ? PackYaml.ParseNumber(m) : null;
                var shift = Get("shift") is { } s ? (int)PackYaml.ParseNumber(s) : 0;
                if (mask is null && shift != 0)
                    throw new FormatException($"{what}: shift needs a mask");
                if (mask is not null && type.Kind == VariableKind.Bcd)
                    throw new FormatException($"{what}: mask and shift don't apply to BCD");
                if (shift is < 0 or > 31)
                    throw new FormatException($"{what}: shift must be 0-31");
                if (mask >= 1L << (type.Size * 8))
                    throw new FormatException($"{what}: mask 0x{mask:X} is wider than {type}'s {type.Size} byte(s)");

                return new Variable(
                    name,
                    PackYaml.ParseNumber(Get(addressKey) ?? throw new FormatException($"{what} has no {addressKey}")),
                    type,
                    Get("description"),
                    Get("verified") is { } verified && bool.Parse(verified),
                    mask,
                    shift);

            default:
                throw new FormatException($"{what} must be '<type> @ <{addressKey}>' or a mapping");
        }
    }

    private static readonly string[] LongFormKeys = ["type", "description", "verified", "mask", "shift"];
    private static readonly string[] ArrayKeys = ["address", "count", "stride", "fields", "description", "verified"];

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
