using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Stgadgets.Cli.Latency;

/// <summary>A rectangle in a video, in its pixels.</summary>
internal readonly record struct Region(int X, int Y, int Width, int Height)
{
    public const string Help = "x,y,width,height in the video's pixels";

    public static Region Parse(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p, CultureInfo.InvariantCulture, out _)))
            throw new UsageException($"'{text}' isn't a region: expected {Help}, e.g. 100,40,300,60");
        var values = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        if (values[0] < 0 || values[1] < 0 || values[2] <= 0 || values[3] <= 0)
            throw new UsageException($"'{text}' isn't a region: x and y can't be negative, and width and height must be above 0");
        return new(values[0], values[1], values[2], values[3]);
    }

    public override string ToString() => $"{X},{Y},{Width},{Height}";
}

/// <summary>
/// Reads how much two regions of a video change from frame to frame, by decoding it with ffmpeg. Every frame is kept
/// with its own timestamp, since screen recordings often have a variable frame rate.
/// </summary>
internal static partial class VideoRegions
{
    /// <summary>How much a pixel's brightness (0–255) must change to count, above video compression noise.</summary>
    public const int PixelThreshold = 24;

    public static IReadOnlyList<FrameChange> Read(string video, Region game, Region gadget)
    {
        var (videoWidth, videoHeight) = Size(video);
        foreach (var (name, region) in (ReadOnlySpan<(string, Region)>)[("--game", game), ("--gadget", gadget)])
        {
            if (region.X + region.Width > videoWidth || region.Y + region.Height > videoHeight)
                throw new UsageException($"{name} {region} is outside the video, which is {videoWidth}x{videoHeight}");
        }

        // Both regions in one picture, the game's above the gadget's, so one decode gives both with the same timestamps.
        var width = Math.Max(game.Width, gadget.Width);
        var filter =
            $"[0:v]format=gray,split=2[a][b];" +
            $"[a]crop={game.Width}:{game.Height}:{game.X}:{game.Y},pad={width}:{game.Height}[game];" +
            $"[b]crop={gadget.Width}:{gadget.Height}:{gadget.X}:{gadget.Y},pad={width}:{gadget.Height}[gadget];" +
            $"[game][gadget]vstack,showinfo";
        using var ffmpeg = Start("ffmpeg",
            "-hide_banner", "-nostats", "-loglevel", "info", "-i", video, "-filter_complex", filter, "-an",
            "-fps_mode", "passthrough", "-f", "rawvideo", "-pix_fmt", "gray", "-");

        // showinfo logs each frame's timestamp to stderr; keep the other lines in case ffmpeg fails.
        var times = new List<double>();
        var messages = new Queue<string>();
        var stderr = Task.Run(() =>
        {
            while (ffmpeg.StandardError.ReadLine() is { } line)
            {
                if (ShowInfo().Match(line) is { Success: true } match)
                {
                    times.Add(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                }
                else if (!line.StartsWith("[Parsed_showinfo", StringComparison.Ordinal))
                {
                    messages.Enqueue(line);
                    if (messages.Count > 10)
                        messages.Dequeue();
                }
            }
        });

        var frameSize = width * (game.Height + gadget.Height);
        var (previous, current) = (new byte[frameSize], new byte[frameSize]);
        var changes = new List<(double Game, double Gadget)>();
        var stdout = ffmpeg.StandardOutput.BaseStream;
        for (var frame = 0; ; frame++)
        {
            var read = stdout.ReadAtLeast(current, frameSize, throwOnEndOfStream: false);
            if (read == 0)
                break;
            if (read < frameSize)
                throw new IOException($"ffmpeg stopped part way through frame {frame}");
            changes.Add(frame == 0
                ? (0, 0)
                : (Changed(previous, current, width, 0, game), Changed(previous, current, width, game.Height, gadget)));
            (previous, current) = (current, previous);
        }

        ffmpeg.WaitForExit();
        stderr.Wait();
        if (ffmpeg.ExitCode != 0)
            throw new IOException($"ffmpeg couldn't read {video}:\n{string.Join('\n', messages)}");
        if (times.Count != changes.Count)
            throw new IOException($"ffmpeg gave {changes.Count} frames but {times.Count} timestamps");
        return times.Zip(changes, (time, change) => new FrameChange(time, change.Game, change.Gadget)).ToList();
    }

    /// <summary>The fraction of a region's pixels that changed, in frames <paramref name="width"/> pixels wide.</summary>
    public static double Changed(ReadOnlySpan<byte> previous, ReadOnlySpan<byte> current, int width, int top, Region region)
    {
        var count = 0;
        for (var y = top; y < top + region.Height; y++)
        {
            for (var x = y * width; x < y * width + region.Width; x++)
            {
                if (Math.Abs(previous[x] - current[x]) > PixelThreshold)
                    count++;
            }
        }

        return (double)count / (region.Width * region.Height);
    }

    private static (int Width, int Height) Size(string video)
    {
        using var ffprobe = Start("ffprobe",
            "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0:s=x", video);
        var output = ffprobe.StandardOutput.ReadToEnd().Trim();
        var error = ffprobe.StandardError.ReadToEnd().Trim();
        ffprobe.WaitForExit();
        var parts = output.Split('x');
        return ffprobe.ExitCode == 0 && parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h)
            ? (w, h)
            : throw new IOException($"ffprobe couldn't read {video}: {(error.Length > 0 ? error : output)}");
    }

    private static Process Start(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        try
        {
            return Process.Start(start) ?? throw new IOException($"Couldn't start {program}");
        }
        catch (Win32Exception)
        {
            throw new UsageException($"{program} isn't installed; it comes with ffmpeg (on macOS: brew install ffmpeg)");
        }
    }

    [GeneratedRegex(@"\bn:\s*\d+\s+pts:\s*\S+\s+pts_time:(\S+)")]
    private static partial Regex ShowInfo();
}
