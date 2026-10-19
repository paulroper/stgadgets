namespace Stgadgets.Core.Memory;

/// <summary>The address ranges a provider should read for every snapshot.</summary>
public sealed record ReadPlan(IReadOnlyList<AddressRange> Ranges)
{
    /// <summary>
    /// Builds a plan that merges ranges which overlap or sit within <paramref name="maxGap"/> bytes of each other.
    /// Each separate read can cost the emulator a round-trip, so fewer, larger reads are better.
    /// </summary>
    public static ReadPlan Create(IEnumerable<AddressRange> ranges, int maxGap = 256)
    {
        var merged = new List<AddressRange>();
        foreach (var range in ranges.OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End + (uint)maxGap)
            {
                var last = merged[^1];
                var end = Math.Max(last.End, range.End);
                merged[^1] = last with { Length = (int)(end - last.Start) };
            }
            else
            {
                merged.Add(range);
            }
        }
        return new ReadPlan(merged);
    }
}
