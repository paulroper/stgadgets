using Stgadgets.Cli.Latency;

namespace Stgadgets.Cli.Tests.Latency;

public sealed class ScoreLagTests
{
    private const double Frame = 1.0 / 60;

    [Fact]
    public void A_change_starts_at_the_first_changed_frame_and_merges_frames_close_after_it()
    {
        // Frames 1 and 2 change (one redraw over two frames), then 3 is quiet, 4 changes again (still the same
        // change: within the merge window of the last changed frame), and 10 is a new change.
        var changed = new[] { 0, 1, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0.001 };
        var frames = changed.Select((c, i) => (i * Frame, (double)c));

        Assert.Equal([1 * Frame, 10 * Frame], ScoreLag.Changes(frames, minChanged: 0.005, merge: 2.5 * Frame));
    }

    [Fact]
    public void Pairs_closest_first_so_a_missed_change_doesnt_shift_the_others()
    {
        // The gadget missed the change at 2.0 s; a greedy in-order match would pair 2.0 with 3.02.
        var pairs = ScoreLag.Pair(game: [1.0, 2.0, 3.0], gadget: [1.02, 3.02], window: 0.25);

        Assert.Equal([new ChangePair(1.0, 1.02), new ChangePair(3.0, 3.02)], pairs);
    }

    [Fact]
    public void Doesnt_pair_changes_further_apart_than_the_window()
    {
        var lag = ScoreLag.Measure(
            [new(0, 0, 0), new(1, 1, 0), new(1.5, 0, 1)], minChanged: 0.5, merge: 0.04, window: 0.25);

        Assert.Empty(lag.Pairs);
        Assert.Equal([1.0], lag.UnpairedGame);
        Assert.Equal([1.5], lag.UnpairedGadget);
    }

    [Fact]
    public void Offsets_are_positive_when_the_gadget_is_behind() =>
        Assert.Equal(-0.5, new ChangePair(Game: 2, Gadget: 1.5).Offset);

    [Theory]
    [InlineData(50, 2)]
    [InlineData(95, 4)]
    [InlineData(100, 4)]
    [InlineData(0, 1)]
    public void Percentiles_are_nearest_rank(double percentile, double expected) =>
        Assert.Equal(expected, ScoreLag.Percentile([4, 1, 3, 2], percentile));
}
