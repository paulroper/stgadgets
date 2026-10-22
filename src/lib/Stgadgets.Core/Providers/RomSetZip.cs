using System.IO.Compression;

namespace Stgadgets.Core.Providers;

/// <summary>An arcade ROM set's zip, which identifies the running game by its chips (docs/memory-maps.md#matching).</summary>
public static class RomSetZip
{
    /// <summary>
    /// The CRC32 of each chip at the top level of the zip, by file name, read from the zip's listing without unpacking
    /// anything. A merged set keeps its clones' chips in subfolders, and those aren't what's running.
    /// </summary>
    /// <returns>Null when the file can't be read as a zip.</returns>
    public static IReadOnlyDictionary<string, uint>? ReadChipCrcs(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            return zip.Entries
                .Where(entry => entry.Name.Length > 0 && entry.FullName == entry.Name)
                .DistinctBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(entry => entry.Name, entry => entry.Crc32, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
