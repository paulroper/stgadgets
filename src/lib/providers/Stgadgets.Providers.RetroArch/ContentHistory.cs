using System.Text.Json;

namespace Stgadgets.Providers.RetroArch;

/// <summary>
/// Finds the running content's file through RetroArch's content history. GET_STATUS only gives the file's name, without
/// its folder or extension, and the history is the one place RetroArch writes the full path.
/// </summary>
internal static class ContentHistory
{
    /// <summary>
    /// Where RetroArch keeps the history by default (builtin/ under its platform drivers' playlist folders), newest
    /// first, so the copy in use wins when there are several installs. A history moved with
    /// content_history_directory isn't found.
    /// </summary>
    /// <param name="executableDirectory">RetroArch's own folder: the default on Windows, where installs are portable.</param>
    public static IEnumerable<string> DefaultLocations(string? executableDirectory)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string?[] playlistFolders =
        [
            Path.Combine(home, "Documents", "RetroArch", "playlists"),
            Path.Combine(home, "Library", "Application Support", "RetroArch", "playlists"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "retroarch", "playlists"),
            Path.Combine(home, ".var", "app", "org.libretro.RetroArch", "config", "retroarch", "playlists"),
            executableDirectory is null ? null : Path.Combine(executableDirectory, "playlists"),
        ];
        return playlistFolders
            .OfType<string>()
            .Select(folder => Path.Combine(folder, "builtin", "content_history.lpl"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc);
    }

    /// <summary>
    /// The newest entry in <paramref name="historyPath"/> whose file, without its extension, is <paramref name="basename"/>.
    /// </summary>
    /// <returns>
    /// Null when there's no such entry, or the history can't be read: RetroArch can also write it in an older line-based
    /// format, or compressed, and those aren't supported.
    /// </returns>
    public static string? FindContent(string historyPath, string basename)
    {
        try
        {
            using var history = JsonDocument.Parse(File.ReadAllBytes(historyPath));
            return history.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.TryGetProperty("path", out var path) ? ArchivePath(path.GetString()) : null)
                .FirstOrDefault(path => path is not null && Path.GetFileNameWithoutExtension(path) == basename);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// For a file inside an archive ("games.zip#game.bin"), the archive, which is what GET_STATUS names; otherwise the path.
    /// </summary>
    private static string? ArchivePath(string? path) => path?.Split('#')[0];
}
