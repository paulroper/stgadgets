using Stgadgets.Core.Derived;
using Stgadgets.Core.Maps;

namespace Stgadgets.Core.Tests;

public sealed class DerivedScriptTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private static readonly MemoryMap Map = MemoryMapLoader.Parse("""
        schema: 1
        system: { endianness: little }
        variables:
          x: u8 @ 0x100
          aliens_remaining: u8 @ 0x101
        """);

    private DerivedScript Load(string js) => DerivedScript.Load(_dir.Write("derived.js", js));

    private static Dictionary<string, object?> Tick(params (string Name, object? Value)[] values) =>
        values.ToDictionary(v => v.Name, v => v.Value);

    [Fact]
    public void Computes_values_in_order_from_the_tick_and_earlier_derived_values()
    {
        var script = Load("""
            export default {
              'aliens.killed': { description: 'Aliens shot this wave', value: v => 55 - v.aliens_remaining },
              'aliens.half_gone': v => v['aliens.killed'] >= 28,
            };
            """);
        Assert.Equal([new DerivedValue("aliens.killed", "Aliens shot this wave"), new DerivedValue("aliens.half_gone")], script.Values);

        using var evaluator = script.CreateEvaluator(Map);
        var values = Tick(("aliens_remaining", 20L));
        Assert.Empty(evaluator.Evaluate(values));
        Assert.Equal(35L, values["aliens.killed"]);
        Assert.Equal(true, values["aliens.half_gone"]);
    }

    [Fact]
    public void Gets_the_previous_ticks_values_including_derived_ones()
    {
        using var evaluator = Load("""
            export default {
              frames_since_kill: (v, prev) =>
                prev === null || v.aliens_remaining < prev.aliens_remaining ? 0 : prev.frames_since_kill + 1,
            };
            """).CreateEvaluator(Map);

        long?[] seen = [.. new long[] { 10, 10, 10, 9, 9 }.Select(aliens =>
        {
            var values = Tick(("aliens_remaining", aliens));
            evaluator.Evaluate(values);
            return (long?)values["frames_since_kill"];
        })];
        Assert.Equal([0, 1, 2, 0, 1], seen);
    }

    [Fact]
    public void Whole_numbers_are_longs_and_other_results_keep_their_type()
    {
        using var evaluator = Load("""
            export default { whole: v => 6 / 2, fraction: v => 1 / 4, text: v => 'danger', none: v => null, flag: v => !v.x };
            """).CreateEvaluator(Map);
        var values = Tick(("x", false));
        Assert.Empty(evaluator.Evaluate(values));
        Assert.Equal(Tick(("x", false), ("whole", 3L), ("fraction", 0.25), ("text", "danger"), ("none", null), ("flag", true)), values);
    }

    [Theory]
    [InlineData("v => { throw new Error('boom'); }", "boom")]
    [InlineData("v => v.misspelt", "undefined")]
    [InlineData("v => v.x / 0", "must return")]
    [InlineData("v => ({ a: 1 })", "must return")]
    [InlineData("v => { while (true) {} }", "")]
    [InlineData("v => importNamespace('System.IO')", "importNamespace")]
    public void A_failing_value_is_null_and_reported_without_stopping_the_others(string function, string message)
    {
        using var evaluator = Load($"export default {{ bad: {function}, good: v => v.x + 1 }};").CreateEvaluator(Map);
        var values = Tick(("x", 1L));
        var error = Assert.Single(evaluator.Evaluate(values));
        Assert.Equal("bad", error.Name);
        Assert.Contains(message, error.Message);
        Assert.Null(values["bad"]);
        Assert.Equal(2L, values["good"]);
    }

    [Theory]
    [InlineData("export default { a: v => }", "")]
    [InlineData("export default { a: 1 };", "must be a function")]
    [InlineData("export const a = v => 1;", "export default")]
    [InlineData("export default { a: { description: 'no value' } };", "must be a function")]
    [InlineData("export default { a: { value: v => 1, descripton: 'typo' } };", "unknown key 'descripton'")]
    [InlineData("export default { a: { value: v => 1, description: 7 } };", "description must be a string")]
    public void Rejects_bad_scripts_on_load(string js, string message)
    {
        var ex = Assert.Throws<FormatException>(() => Load(js));
        Assert.Contains("derived.js", ex.Message);
        Assert.Contains(message, ex.Message);
    }
}
