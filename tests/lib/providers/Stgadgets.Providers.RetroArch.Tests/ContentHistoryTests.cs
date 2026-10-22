namespace Stgadgets.Providers.RetroArch.Tests;

public sealed class ContentHistoryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("stgadgets-retroarch-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Finds_the_newest_entry_with_the_name()
    {
        var history = WriteHistory("""
            {
              "version": "1.5",
              "items": [
                { "path": "/roms/arcade/ddonpach.zip", "core_name": "Arcade (FinalBurn Neo)" },
                { "path": "/roms/new/invaders.zip", "core_name": "Arcade (FinalBurn Neo)" },
                { "path": "/roms/old/invaders.zip", "core_name": "Arcade (FinalBurn Neo)" }
              ]
            }
            """);

        Assert.Equal("/roms/new/invaders.zip", ContentHistory.FindContent(history, "invaders"));
        Assert.Null(ContentHistory.FindContent(history, "esprade"));
    }

    [Fact]
    public void Gives_the_archive_for_a_file_inside_one()
    {
        var history = WriteHistory("""{ "items": [{ "path": "/roms/games.zip#game.bin" }] }""");
        Assert.Equal("/roms/games.zip", ContentHistory.FindContent(history, "games"));
    }

    [Fact]
    public void Gives_null_for_a_history_in_the_old_format()
    {
        var history = WriteHistory("/roms/arcade/invaders.zip\ninvaders\n/cores/fbneo_libretro.dylib\nDETECT\n");
        Assert.Null(ContentHistory.FindContent(history, "invaders"));
    }

    [Fact]
    public void Looks_in_retroarchs_own_folder()
    {
        var history = WriteHistory("{}");
        Assert.Contains(history, ContentHistory.DefaultLocations(_directory));
    }

    private string WriteHistory(string contents)
    {
        var path = Path.Combine(_directory, "playlists", "builtin", "content_history.lpl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }
}
