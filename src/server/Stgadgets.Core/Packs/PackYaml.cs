using System.Globalization;
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
}
