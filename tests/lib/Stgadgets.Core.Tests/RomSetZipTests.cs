using System.IO.Compression;
using System.Text;
using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Tests;

public sealed class RomSetZipTests : IDisposable
{
    private readonly TempDirectory _roms = new();

    public void Dispose() => _roms.Dispose();

    [Fact]
    public void Reads_the_crcs_of_the_chips_at_the_top_level()
    {
        var path = WriteZip(("prg.bin", "program"), ("clone/prg.bin", "clone program"), ("clone/", ""));

        var crcs = RomSetZip.ReadChipCrcs(path);

        Assert.Equal(new Dictionary<string, uint> { ["prg.bin"] = 0x92ed7784 }, crcs);
    }

    [Fact]
    public void Gives_null_for_a_file_thats_not_a_zip() =>
        Assert.Null(RomSetZip.ReadChipCrcs(_roms.Write("game.bin", "not a zip")));

    [Fact]
    public void Gives_null_for_a_missing_file() =>
        Assert.Null(RomSetZip.ReadChipCrcs(Path.Combine(_roms.Path, "missing.zip")));

    private string WriteZip(params (string Name, string Contents)[] entries)
    {
        var path = Path.Combine(_roms.Path, "game.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, contents) in entries)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(Encoding.ASCII.GetBytes(contents));
        }
        return path;
    }
}
