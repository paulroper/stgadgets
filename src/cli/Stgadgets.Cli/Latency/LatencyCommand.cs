using System.CommandLine;
using System.Globalization;

namespace Stgadgets.Cli.Latency;

/// <summary><c>stgadgets latency</c>: latency tests (docs/cli.md#latency, docs/latency.md).</summary>
internal static class LatencyCommand
{
    public static Command Create() => new("latency", "Measure how far the overlay is behind the game.")
    {
        Analyse(),
    };

    /// <summary><c>stgadgets latency analyse</c>: the end-to-end test (L2) on a screen recording.</summary>
    private static Command Analyse()
    {
        var video = new Argument<FileInfo>("video") { Description = "A screen recording of the game and the overlay, at 60 fps or more." };
        var game = new Option<string>("--game") { Description = $"The game's own score on screen: {Region.Help}.", Required = true };
        var gadget = new Option<string>("--gadget") { Description = $"The score gadget: {Region.Help}.", Required = true };
        var refresh = new Option<double>("--refresh") { Description = "The display's refresh rate, to count frames in.", DefaultValueFactory = _ => 60 };
        var minChanged = new Option<double>("--min-changed")
        {
            Description = "The percentage of a region's pixels that must change for it to count as a change.",
            DefaultValueFactory = _ => 0.5,
        };
        var window = new Option<double>("--window")
        {
            Description = "How far apart, in ms, a game change and a gadget change can be and still be paired.",
            DefaultValueFactory = _ => 250,
        };
        var events = new Option<bool>("--events") { Description = "List every change and pair, to check against the video." };
        var command = new Command("analyse", "Measure how far the score gadget is behind the game's own score in a screen recording.")
        {
            video, game, gadget, refresh, minChanged, window, events,
        };
        command.SetAction(StgadgetsCommand.Handle(result =>
        {
            var file = result.GetRequiredValue(video);
            if (!file.Exists)
                throw new UsageException($"{file.FullName} doesn't exist");
            var frames = VideoRegions.Read(
                file.FullName, Region.Parse(result.GetRequiredValue(game)), Region.Parse(result.GetRequiredValue(gadget)));
            var frame = 1 / result.GetValue(refresh);
            var lag = ScoreLag.Measure(frames, result.GetValue(minChanged) / 100, merge: 2.5 * frame, result.GetValue(window) / 1000);
            Report(result.InvocationConfiguration.Output, lag, frames.Count, result.GetValue(refresh), result.GetValue(events));
        }));
        return command;
    }

    private static void Report(TextWriter output, ScoreLag lag, int frameCount, double refresh, bool events)
    {
        var frame = 1 / refresh;
        string Ms(double seconds) => (seconds * 1000).ToString("F1", CultureInfo.InvariantCulture) + " ms";
        string Frames(double seconds) => (seconds / frame).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
        string Time(double seconds) => seconds.ToString("F3", CultureInfo.InvariantCulture) + " s";

        var interval = ScoreLag.Percentile(lag.FrameIntervals, 50);
        output.WriteLine($"video: {frameCount} frames, one every {Ms(interval)} (p50), {Ms(ScoreLag.Percentile(lag.FrameIntervals, 95))} (p95)");
        if (interval > 1.2 * frame)
            output.WriteLine($"warning: the video has fewer frames than the display's {refresh} Hz, so gaps are only measured to about {Ms(interval)}");
        output.WriteLine($"changes: game {lag.GameChanges.Count}, gadget {lag.GadgetChanges.Count}; paired {lag.Pairs.Count}");

        if (lag.Pairs.Count > 0)
        {
            var offsets = lag.Pairs.Select(p => p.Offset).ToList();
            var gaps = offsets.Select(Math.Abs).ToList();
            output.WriteLine();
            output.WriteLine($"gadget behind the game (+) or ahead (-), in frames at {refresh} Hz:");
            output.WriteLine($"  offset   p50 {Frames(ScoreLag.Percentile(offsets, 50))}   p95 {Frames(ScoreLag.Percentile(offsets, 95))}   " +
                             $"min {Frames(offsets.Min())}   max {Frames(offsets.Max())}");
            output.WriteLine($"  gap      p50 {Frames(ScoreLag.Percentile(gaps, 50))}   p95 {Frames(ScoreLag.Percentile(gaps, 95))}   " +
                             $"max {Frames(gaps.Max())}   ({Ms(ScoreLag.Percentile(gaps, 50))} / {Ms(ScoreLag.Percentile(gaps, 95))} / {Ms(gaps.Max())})");
            var counts = offsets.GroupBy(o => (int)Math.Round(o / frame)).OrderBy(g => g.Key)
                .Select(g => $"{g.Key.ToString("+0;-0;0", CultureInfo.InvariantCulture)}: {g.Count()}");
            output.WriteLine($"  by frame {string.Join("   ", counts)}");
        }

        var (unpairedGame, unpairedGadget) = (lag.UnpairedGame.ToList(), lag.UnpairedGadget.ToList());
        if (unpairedGame.Count + unpairedGadget.Count > 0)
        {
            output.WriteLine();
            if (unpairedGame.Count > 0)
                output.WriteLine($"unpaired game changes at {string.Join(", ", unpairedGame.Select(Time))}");
            if (unpairedGadget.Count > 0)
                output.WriteLine($"unpaired gadget changes at {string.Join(", ", unpairedGadget.Select(Time))}");
        }

        if (events)
        {
            output.WriteLine();
            output.WriteLine("      game     gadget   offset");
            foreach (var pair in lag.Pairs)
                output.WriteLine($"{Time(pair.Game),10} {Time(pair.Gadget),10} {Frames(pair.Offset),8}");
        }
    }
}
