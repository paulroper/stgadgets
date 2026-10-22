using Stgadgets.Core.Derived;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;

namespace Stgadgets.Core.Tests;

public sealed class PackLoaderTests : IDisposable
{
    private readonly TempDirectory _packs = new();

    public PackLoaderTests()
    {
        _packs.Write("test/pack.yaml", """
            schema: 1
            id: test
            title: Test Game
            year: 1997
            match: { romsets: [test, testj], program_crcs: { prg.bin: 0x1234abcd } }
            """);
        _packs.Write("test/map.yaml", """
            schema: 1
            system: { endianness: big }
            variables:
              p1.lives: u8 @ 0x1000
            """);
        _packs.Write("test/providers/someemu.yaml", "cores: {}");
    }

    public void Dispose() => _packs.Dispose();

    [Fact]
    public void Loads_pack_map_and_provider_settings()
    {
        var pack = PackLoader.Load(Path.Combine(_packs.Path, "test"));

        Assert.Equal("test", pack.Id);
        Assert.Equal(new GameInfo("Test Game", null, 1997, null), pack.Game);
        Assert.Equal(0x1234abcdu, pack.ProgramCrcs["prg.bin"]);
        Assert.Equal("p1.lives", Assert.Single(pack.Map.Variables).Name);
        Assert.Equal("cores: {}", pack.ProviderSettings["someemu"].Yaml);
    }

    [Fact]
    public void Matches_romset_names_case_insensitively()
    {
        var catalog = PackCatalog.LoadDirectory(_packs.Path);
        Assert.Equal("test", catalog.Find(new ContentInfo("TESTJ", "fb_alpha"))?.Id);
        Assert.Null(catalog.Find(new ContentInfo("other", "fb_alpha")));
    }

    [Fact]
    public void Matches_program_rom_crcs_whatever_the_romset_is_called()
    {
        var catalog = PackCatalog.LoadDirectory(_packs.Path);
        Assert.Equal("test", catalog.Find(new ContentInfo("renamed", "fb_alpha", Chips(("prg_alt.bin", 0x1234abcd), ("gfx.bin", 1))))?.Id);
    }

    [Fact]
    public void Doesnt_match_another_revision_under_the_same_name()
    {
        var catalog = PackCatalog.LoadDirectory(_packs.Path);
        Assert.Null(catalog.Find(new ContentInfo("test", "fb_alpha", Chips(("prg.bin", 0x99999999)))));
    }

    [Fact]
    public void Matches_by_name_when_the_pack_lists_no_crcs()
    {
        _packs.Write("test/pack.yaml", """
            schema: 1
            id: test
            title: Test Game
            match: { romsets: [test] }
            """);
        var catalog = PackCatalog.LoadDirectory(_packs.Path);
        Assert.Equal("test", catalog.Find(new ContentInfo("test", "fb_alpha", Chips(("prg.bin", 0x99999999))))?.Id);
    }

    private static Dictionary<string, uint> Chips(params (string Name, uint Crc)[] chips) =>
        chips.ToDictionary(chip => chip.Name, chip => chip.Crc);

    [Fact]
    public void Pack_id_must_match_the_folder_name()
    {
        Directory.Move(Path.Combine(_packs.Path, "test"), Path.Combine(_packs.Path, "renamed"));
        var ex = Assert.Throws<FormatException>(() => PackLoader.Load(Path.Combine(_packs.Path, "renamed")));
        Assert.Contains("folder name", ex.Message);
    }

    [Fact]
    public void Loads_derived_js_when_present()
    {
        Assert.Null(PackLoader.Load(Path.Combine(_packs.Path, "test")).Derived);

        _packs.Write("test/derived.js", "export default { 'p1.ships_reserve': v => v['p1.lives'] - 1 };");
        Assert.Equal([new DerivedValue("p1.ships_reserve")], PackLoader.Load(Path.Combine(_packs.Path, "test")).Derived?.Values);
    }

    [Fact]
    public void Derived_values_cant_reuse_a_map_variables_name()
    {
        _packs.Write("test/derived.js", "export default { 'p1.lives': v => 3 };");
        var ex = Assert.Throws<FormatException>(() => PackLoader.Load(Path.Combine(_packs.Path, "test")));
        Assert.Contains("already a variable", ex.Message);
    }

    [Fact]
    public void Loads_every_pack_in_the_repo() =>
        Assert.NotEmpty(PackCatalog.LoadDirectory(RepoPaths.Packs).Packs);
}
