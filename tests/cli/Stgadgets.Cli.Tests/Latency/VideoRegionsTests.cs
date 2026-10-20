using Stgadgets.Cli.Latency;
using static Stgadgets.Cli.Tests.Recordings.RecordingFixture;

namespace Stgadgets.Cli.Tests.Latency;

public sealed class VideoRegionsTests
{
    [Fact]
    public void Changed_counts_pixels_in_the_region_that_changed_by_more_than_compression_noise()
    {
        // Two regions stacked in a 4-pixel-wide frame: a 2x2 one in rows 0–1, and a 4x1 one in row 2.
        byte[] previous = [10, 10, 99, 99, 10, 10, 99, 99, 10, 10, 10, 10];
        byte[] current = [200, 20, 0, 0, 10, 10, 0, 0, 10, 10, 200, 200];

        Assert.Equal(0.25, VideoRegions.Changed(previous, current, width: 4, top: 0, new Region(0, 0, 2, 2)));
        Assert.Equal(0.5, VideoRegions.Changed(previous, current, width: 4, top: 2, new Region(0, 0, 4, 1)));
    }

    [Theory]
    [InlineData("10,20,300,40", 10, 20, 300, 40)]
    [InlineData(" 0, 0, 1, 1 ", 0, 0, 1, 1)]
    public void Parses_regions(string text, int x, int y, int width, int height) =>
        Assert.Equal(new Region(x, y, width, height), Region.Parse(text));

    [Theory]
    [InlineData("10,20,300")]
    [InlineData("10,20,0,40")]
    [InlineData("-1,20,300,40")]
    [InlineData("a,b,c,d")]
    public void Says_when_a_region_isnt_one(string text)
    {
        using var video = new TempDirectory();
        var file = video.Write("score.mov", "");

        var (exitCode, _, error) = Run("latency", "analyse", file, "--game", text, "--gadget", "0,0,1,1");

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"'{text}' isn't a region", error);
    }
}
