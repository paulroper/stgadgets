using Stgadgets.Core.Derived;
using Stgadgets.Core.Maps;

namespace Stgadgets.Core.Recordings;

/// <summary>One tick of a replay: every value the server would publish, and the derived values that failed.</summary>
public sealed record ReplayedTick(int Tick, Dictionary<string, object?> Values, IReadOnlyList<DerivedError> Errors);

/// <summary>Plays a recording back as the server would play the game: each tick decoded through the map, then derived.js.</summary>
public static class RecordingReplay
{
    /// <summary>
    /// Each tick's values in order. Derived values are evaluated with <see cref="DerivedScript.ReplayTickLimit"/>, so a
    /// busy machine doesn't fail a tick that's merely slow.
    /// </summary>
    public static IEnumerable<ReplayedTick> Run(Recording recording, MapDecoder decoder, DerivedScript? derived = null)
    {
        using var evaluator = derived?.CreateEvaluator(decoder.Map, DerivedScript.ReplayTickLimit);
        for (var tick = 0; tick < recording.Count; tick++)
        {
            var values = decoder.Decode(recording[tick]);
            var errors = evaluator?.Evaluate(values) ?? [];
            yield return new ReplayedTick(tick, values, errors);
        }
    }
}
