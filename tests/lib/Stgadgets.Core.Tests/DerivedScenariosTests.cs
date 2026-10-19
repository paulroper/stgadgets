using Stgadgets.Core.Derived;
using Stgadgets.Core.Maps;

namespace Stgadgets.Core.Tests;

/// <summary>The scenario runner itself, on a map with two variables and a derived.js that counts and averages.</summary>
public sealed class DerivedScenariosTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private static readonly MemoryMap Map = MemoryMapLoader.Parse("""
        schema: 1
        system: { endianness: little }
        variables:
          x: u8 @ 0x100
          flag: u8 @ 0x101
        """);

    private readonly DerivedScript _derived;

    public DerivedScenariosTests()
    {
        _derived = DerivedScript.Load(_dir.Write("derived.js", """
            export default {
              doubled: v => v.x * 2,
              third: v => v.x / 3,
              ticks: (v, prev) => prev === null ? 1 : prev.ticks + 1,
              broken: v => { if (v.flag) throw new Error('flag set'); return 0; },
            };
            """));
    }

    public void Dispose() => _dir.Dispose();

    private IReadOnlyList<string> Check(string yaml) => DerivedScenarios.Parse(yaml).Check(Map, _derived);

    [Fact]
    public void Passes_when_values_match() =>
        Assert.Empty(Check("""
            start: { x: 3 }
            scenarios:
              counting:
                - expect: { x: 3, doubled: 6, ticks: 1, flag: 0 }
                - set: { x: 0x10 }
                  repeat: 3
                  expect: { doubled: 32, ticks: 4 }
            """));

    [Fact]
    public void Adds_to_variables_on_every_tick_after_the_first() =>
        Assert.Empty(Check("""
            add: { x: 1 }
            scenarios:
              counting up:
                - expect: { x: 0 }
                - repeat: 2
                  expect: { x: 2 }
                - add: { x: 10 }
                  expect: { x: 13 }
                - set: { x: 100 }
                  expect: { x: 100 }
            """));

    [Fact]
    public void Starts_each_scenario_afresh_from_the_start_values() =>
        Assert.Empty(Check("""
            start: { x: 3 }
            scenarios:
              first:
                - set: { x: 5 }
                  repeat: 2
                  expect: { ticks: 2 }
              second:
                - expect: { x: 3, ticks: 1 }
            """));

    [Fact]
    public void Matches_fractions_to_the_decimal_places_written() =>
        Assert.Equal(
            ["'thirds' step 3 (tick 2): third is 0.3333333333333333, expected 0.34"],
            Check("""
                start: { x: 1 }
                scenarios:
                  thirds:
                    - expect: { third: 0.333 }
                    - expect: { third: 0.33 }
                    - expect: { third: 0.34 }
                """));

    [Fact]
    public void Reports_wrong_values_and_failed_derived_values() =>
        Assert.Equal(
            [
                "'wrong' step 1 (tick 0): doubled is 0, expected 1",
                "'wrong' step 2 (tick 1): derived 'broken' failed: flag set",
                "'wrong' step 2 (tick 1): broken is null, expected 0",
            ],
            Check("""
                scenarios:
                  wrong:
                    - expect: { doubled: 1 }
                    - set: { flag: 1 }
                      expect: { broken: 0 }
                """));

    [Fact]
    public void Reports_names_that_cant_be_set_or_checked() =>
        Assert.Equal(
            [
                "start: the map has no variable 'y'",
                "'typos' step 1: 'doubled' is a derived value; set the map variables it comes from",
                "'typos' step 1: no map variable or derived value 'tripled'",
            ],
            Check("""
                start: { y: 1 }
                scenarios:
                  typos:
                    - set: { doubled: 4 }
                      expect: { tripled: 6 }
                """));

    [Fact]
    public void Still_stops_a_runaway_script()
    {
        var runaway = DerivedScript.Load(_dir.Write("runaway.js", "export default { stuck: v => { while (true) {} } };"));

        var failures = DerivedScenarios.Parse("""
            scenarios:
              stuck:
                - expect: { stuck: null }
            """).Check(Map, runaway);

        Assert.Equal(["'stuck' step 1 (tick 0): derived 'stuck' failed: Script ran for more than 1000 ms"], failures);
    }

    [Fact]
    public void Reports_a_scenario_that_expects_nothing() =>
        Assert.Equal(["'empty': expects nothing"], Check("""
            scenarios:
              empty:
                - set: { x: 1 }
            """));
}
