using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Tests;

public sealed class PackLibraryTests : IDisposable
{
    private readonly TempDirectory _packs = new();

    public PackLibraryTests()
    {
        _packs.Write("test/pack.yaml", "schema: 1\nid: test\ntitle: Test Game\nmatch: { romsets: [test] }");
        WriteMap("x");
    }

    public void Dispose() => _packs.Dispose();

    private void WriteMap(string variable) =>
        _packs.Write("test/map.yaml", $"schema: 1\nsystem: {{ endianness: little }}\nvariables:\n  {variable}: u8 @ 0x100\n");

    private static string Variable(PackLibrary library) => Assert.Single(Assert.Single(library.Catalog.Packs).Map.Variables).Name;

    [Theory]
    [InlineData("ddonpach/pack.yaml", true)]
    [InlineData("ddonpach/map.yaml", true)]
    [InlineData("ddonpach/derived.js", true)]
    [InlineData("ddonpach/providers/retroarch.yaml", true)]
    [InlineData("ddonpach/recordings/play-01/ram.bin", false)]
    [InlineData("ddonpach/ui/layout.tsx", false)]
    [InlineData("ddonpach/tests/derived/rank.yaml", false)]
    [InlineData("ddonpach/NOTES.md", false)]
    [InlineData("ddonpach", false)]
    public void Knows_which_files_a_pack_is_loaded_from(string path, bool isPackFile) =>
        Assert.Equal(isPackFile, PackLibrary.IsPackFile(path));

    [Fact]
    public void Finds_when_a_pack_file_was_last_written()
    {
        _packs.Write("test/providers/retroarch.yaml", "cores: {}");
        _packs.Write("test/recordings/run/ram.bin", [1]);
        File.SetLastWriteTimeUtc(Path.Combine(_packs.Path, "test", "pack.yaml"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(_packs.Path, "test", "map.yaml"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(_packs.Path, "test", "providers", "retroarch.yaml"), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(_packs.Path, "test", "recordings", "run", "ram.bin"), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), PackLibrary.NewestPackFile(_packs.Path));
    }

    [Fact]
    public void A_reload_swaps_in_the_new_packs_and_says_so()
    {
        using var library = new PackLibrary(_packs.Path, watch: false);
        var changed = library.Changed;
        var before = library.Status();
        WriteMap("y");

        library.Reload();

        Assert.Equal("y", Variable(library));
        Assert.True(changed.IsCancellationRequested);
        Assert.False(library.Changed.IsCancellationRequested);
        Assert.Equal((2, 2), (library.Version, library.Status().Version));
        Assert.True(library.Status().ReadAt >= before.ReadAt);
    }

    [Fact]
    public void A_reload_that_fails_keeps_the_last_packs_until_one_works()
    {
        var failures = new List<Exception>();
        using var library = new PackLibrary(_packs.Path, watch: false, failures.Add);
        var changed = library.Changed;
        _packs.Write("test/map.yaml", "variables: [");

        library.Reload();

        Assert.Equal("x", Variable(library));
        Assert.NotNull(library.Error);
        Assert.Single(failures);
        Assert.False(changed.IsCancellationRequested);
        Assert.Equal((1, library.Error), (library.Status().Version, library.Status().Error));

        WriteMap("y");
        library.Reload();
        Assert.Null(library.Error);
    }

    [Fact]
    public async Task Reloads_when_a_pack_file_changes_if_watching()
    {
        using var library = new PackLibrary(_packs.Path, watch: true);
        var changed = library.Changed;

        WriteMap("y");

        await Task.Delay(TimeSpan.FromSeconds(10), changed).ContinueWith(_ => { }, TaskScheduler.Default);
        Assert.True(changed.IsCancellationRequested);
        Assert.Equal("y", Variable(library));
    }
}
