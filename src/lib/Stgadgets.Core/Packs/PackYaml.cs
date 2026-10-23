using System.Globalization;
using Stgadgets.Core.Maps;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Stgadgets.Core.Packs;

/// <summary>
/// The YAML conventions shared by every file in a pack: snake_case keys, and numbers read as strings so hex like
/// 0x20F8 parses the same everywhere (<see cref="ParseNumber"/>).
/// </summary>
public static class PackYaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static T Deserialize<T>(string yaml) =>
        Deserializer.Deserialize<T>(yaml) ?? throw new FormatException("Empty file");

    /// <summary>Reads and deserializes a file, adding its path to any error.</summary>
    public static T LoadFile<T>(string path, Func<string, T> parse)
    {
        try
        {
            return parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is FormatException or YamlDotNet.Core.YamlException)
        {
            throw new FormatException($"{path}: {ex.Message}", ex);
        }
    }

    /// <summary>Parses a decimal or <c>0x</c> hex number.</summary>
    public static uint ParseNumber(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.Parse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : uint.Parse(text, CultureInfo.InvariantCulture);

    /// <summary>
    /// A value in a pack's test files, as the decoder and derived.js produce them: <see cref="long"/>, <see cref="bool"/>
    /// or null. Derived values can also be strings or fractions (<see cref="double"/>).
    /// </summary>
    public static object? ParseValue(string? text) => text switch
    {
        null or "null" or "~" => null,
        "true" => true,
        "false" => false,
        _ when text.StartsWith('-') && IsNumber(text[1..]) => -(long)ParseNumber(text[1..]),
        _ when IsNumber(text) => (long)ParseNumber(text),
        _ when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => text,
    };

    /// <summary>A value for a message, in the form <see cref="ParseValue"/> reads.</summary>
    public static string FormatValue(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        string s => $"'{s}'",
        ArrayValue array => $"[{array.Count} entries]",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };

    private static bool IsNumber(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            : uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
