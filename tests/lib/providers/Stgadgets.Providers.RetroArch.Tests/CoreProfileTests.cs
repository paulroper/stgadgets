using Stgadgets.Core.Memory;
using Stgadgets.Core.Providers;

namespace Stgadgets.Providers.RetroArch.Tests;

public sealed class CoreProfileTests
{
    [Fact]
    public void Reads_a_single_block_from_ram_base()
    {
        var profile = Find("""
            schema: 1
            cores:
              fb_alpha: { ram_base: 0x2000, ram_size: 0x2000 }
            """);

        Assert.Equal([new CoreRead(0, 0x10, 0x100)], profile.ToReads(new AddressRange(0x2100, 0x10)));
    }

    [Fact]
    public void Reads_each_segment_from_its_own_offset()
    {
        var profile = Find("""
            schema: 1
            cores:
              fb_alpha:
                word_swapped: true
                segments:
                  - { address: 0x203000, offset: 0x2000, size: 0x5000 }
                  - { address: 0x208000, offset: 0xF000, size: 0x8000 }
            """);

        Assert.True(profile.WordSwapped);
        Assert.Equal([new CoreRead(0, 4, 0x169D0)], profile.ToReads(new AddressRange(0x20F9D0, 4)));
        Assert.Equal([new CoreRead(0, 2, 0x3062)], profile.ToReads(new AddressRange(0x204062, 2)));
    }

    [Fact]
    public void Splits_a_range_that_crosses_into_the_next_segment()
    {
        var profile = new CoreProfile([new CoreSegment(0x203000, 0x2000, 0x5000), new CoreSegment(0x208000, 0xF000, 0x8000)]);

        Assert.Equal(
            [new CoreRead(0, 0x10, 0x6FF0), new CoreRead(0x10, 0x10, 0xF000)],
            profile.ToReads(new AddressRange(0x207FF0, 0x20)));
    }

    [Fact]
    public void Rejects_a_range_the_core_does_not_expose()
    {
        var profile = new CoreProfile([new CoreSegment(0x203000, 0x2000, 0x5000), new CoreSegment(0x208000, 0xF000, 0x8000)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.ToReads(new AddressRange(0x202FF0, 0x20)).ToList());
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.ToReads(new AddressRange(0x20FFF0, 0x20)).ToList());
    }

    [Fact]
    public void Needs_word_aligned_segments_when_words_are_swapped()
    {
        Assert.Throws<FormatException>(() => Find("""
            schema: 1
            cores:
              fb_alpha:
                word_swapped: true
                segments: [{ address: 0x203001, offset: 0x2000, size: 0x10 }]
            """));
    }

    [Fact]
    public void Needs_either_a_block_or_segments()
    {
        Assert.Throws<FormatException>(() => Find("""
            schema: 1
            cores:
              fb_alpha:
                ram_base: 0x2000
                ram_size: 0x2000
                segments: [{ address: 0x2000, offset: 0, size: 0x10 }]
            """));
        Assert.Throws<FormatException>(() => Find("""
            schema: 1
            cores:
              fb_alpha: { ram_base: 0x2000 }
            """));
    }

    private static CoreProfile Find(string yaml) =>
        CoreProfile.Find(new ProviderSettings("retroarch.yaml", yaml), "fb_alpha") ?? throw new InvalidOperationException("no profile");
}
