namespace Stgadgets.Cli.Latency;

/// <summary>How much of each region changed since the video's previous frame: a fraction from 0 to 1.</summary>
/// <param name="Time">When the frame is shown, in seconds from the start of the video.</param>
internal readonly record struct FrameChange(double Time, double Game, double Gadget);

/// <summary>A change to the game's score and the gadget's change that goes with it, in seconds.</summary>
internal readonly record struct ChangePair(double Game, double Gadget)
{
    /// <summary>How far the gadget is behind the game: negative when it's ahead.</summary>
    public double Offset => Gadget - Game;
}

/// <summary>
/// The end-to-end latency test (requirement L2, docs/requirements.md): finds when the game's score and the gadget's
/// score change in a screen recording, and pairs them up.
/// </summary>
internal sealed record ScoreLag(
    IReadOnlyList<double> GameChanges, IReadOnlyList<double> GadgetChanges, IReadOnlyList<ChangePair> Pairs,
    IReadOnlyList<double> FrameIntervals)
{
    public static ScoreLag Measure(IReadOnlyList<FrameChange> frames, double minChanged, double merge, double window)
    {
        var game = Changes(frames.Select(f => (f.Time, f.Game)), minChanged, merge);
        var gadget = Changes(frames.Select(f => (f.Time, f.Gadget)), minChanged, merge);
        var intervals = frames.Zip(frames.Skip(1), (a, b) => b.Time - a.Time).ToList();
        return new(game, gadget, Pair(game, gadget, window), intervals);
    }

    /// <summary>
    /// When a region starts changing. Changed frames less than <paramref name="merge"/> seconds after the last one
    /// are part of the same change, such as a redraw that takes two frames.
    /// </summary>
    public static IReadOnlyList<double> Changes(IEnumerable<(double Time, double Changed)> frames, double minChanged, double merge)
    {
        var changes = new List<double>();
        double? last = null;
        foreach (var (time, changed) in frames)
        {
            if (changed < minChanged)
                continue;
            if (last is not { } previous || time - previous > merge)
                changes.Add(time);
            last = time;
        }
        return changes;
    }

    /// <summary>
    /// Pairs each game change with a gadget change no more than <paramref name="window"/> seconds away, closest pairs
    /// first, so a change missed on one side doesn't shift every pair after it.
    /// </summary>
    public static IReadOnlyList<ChangePair> Pair(IReadOnlyList<double> game, IReadOnlyList<double> gadget, double window)
    {
        var candidates =
            from g in Enumerable.Range(0, game.Count)
            from d in Enumerable.Range(0, gadget.Count)
            let gap = Math.Abs(gadget[d] - game[g])
            where gap <= window
            orderby gap
            select (g, d);
        var (usedGame, usedGadget) = (new HashSet<int>(), new HashSet<int>());
        var pairs = new List<ChangePair>();
        foreach (var (g, d) in candidates)
        {
            if (!usedGame.Contains(g) && !usedGadget.Contains(d))
            {
                usedGame.Add(g);
                usedGadget.Add(d);
                pairs.Add(new(game[g], gadget[d]));
            }
        }

        return pairs.OrderBy(p => p.Game).ToList();
    }

    public IEnumerable<double> UnpairedGame => GameChanges.Except(Pairs.Select(p => p.Game));

    public IEnumerable<double> UnpairedGadget => GadgetChanges.Except(Pairs.Select(p => p.Gadget));

    /// <summary>The nearest-rank percentile (0 to 100) of some values.</summary>
    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
            return double.NaN;
        var rank = (int)Math.Ceiling(percentile / 100 * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }
}
