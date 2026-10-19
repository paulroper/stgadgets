using Stgadgets.Core.Derived;
using Stgadgets.Core.Packs;

namespace Stgadgets.Core.Tests;

/// <summary>Runs every pack's derived.js scenarios (packs/*/tests/derived/*.yaml), one test per file.</summary>
public class PackDerivedScenarioTests
{
    public static TheoryData<string, string> ScenarioFiles()
    {
        var files = new TheoryData<string, string>();
        foreach (var pack in PackCatalog.LoadDirectory(RepoPaths.Packs).Packs)
        {
            foreach (var file in pack.DerivedScenarios)
                files.Add(pack.Id, Path.GetFileName(file));
        }
        return files;
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public void Derived_values_match_the_scenarios(string packId, string file)
    {
        var pack = PackCatalog.LoadDirectory(RepoPaths.Packs).Packs.Single(p => p.Id == packId);
        Assert.NotNull(pack.Derived);

        var failures = DerivedScenarios.Load(Path.Combine(pack.DirectoryPath, "tests", "derived", file)).Check(pack.Map, pack.Derived);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
