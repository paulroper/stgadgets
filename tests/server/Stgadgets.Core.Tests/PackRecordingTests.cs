using Stgadgets.Core.Maps;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Recordings;
using Xunit.Abstractions;

namespace Stgadgets.Core.Tests;

/// <summary>
/// Replays every recording found under packs/*/recordings through its pack's map and checks its expected.yaml.
/// Recordings are local and git-ignored (docs/decisions/0011-recordings-are-local.md), so a fresh clone has none
/// and this passes trivially; with recordings present it checks all of them.
/// </summary>
public class PackRecordingTests(ITestOutputHelper output)
{
    [Fact]
    public void Local_recordings_decode_to_expected_values()
    {
        var failures = new List<string>();
        foreach (var pack in PackCatalog.LoadDirectory(RepoPaths.Packs).Packs)
        {
            foreach (var directory in pack.Recordings)
            {
                // Without expected.yaml this still checks that every tick decodes (docs/packs.md#testing).
                var name = $"{pack.Id}/{Path.GetFileName(directory)}";
                var problems = ExpectedValues.Load(directory).Check(Recording.Load(directory), new MapDecoder(pack.Map));
                output.WriteLine($"{name}: {(problems.Count == 0 ? "ok" : $"{problems.Count} failure(s)")}");
                failures.AddRange(problems.Select(p => $"{name}: {p}"));
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
