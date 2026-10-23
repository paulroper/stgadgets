using Stgadgets.Core.Derived;
using Stgadgets.Core.Maps;
using Stgadgets.Core.Memory;

namespace Stgadgets.Core.Tests;

/// <summary>Array variables, on a table of three 8-byte entries at 0x200: a status word, and HP at offset 4.</summary>
public sealed class ArrayVariableTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private static readonly MemoryMap Map = MemoryMapLoader.Parse("""
        schema: 1
        system: { endianness: big }
        variables:
          stage: u8 @ 0x100
          enemies:
            address: 0x200
            count: 3
            stride: 8
            description: Enemies on screen
            fields:
              status: u16 @ 0
              hp: u16 @ 4
              active: { type: bool, offset: 0, mask: 0x80 }
        """);

    // Entry 0 active with 300 HP, entry 1 empty, entry 2 active with 5 HP.
    private static readonly byte[] Table =
    [
        0x80, 0x00, 0, 0, 0x01, 0x2C, 0, 0,
        0x00, 0x00, 0, 0, 0x00, 0x00, 0, 0,
        0x80, 0x01, 0, 0, 0x00, 0x05,
    ];

    private static Snapshot Snapshot(byte[] table) =>
        new(null, TimeSpan.Zero, [new MemoryBlock(new AddressRange(0x100, 1), new byte[] { 2 }), new MemoryBlock(new AddressRange(0x200, table.Length), table)]);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Parses_an_array_with_short_and_long_form_fields()
    {
        var enemies = Assert.Single(Map.Arrays);

        Assert.Equal(("enemies", 0x200u, 3, 8, "Enemies on screen"), (enemies.Name, enemies.Address, enemies.Count, enemies.Stride, enemies.Description));
        Assert.Equal(
            [
                new Variable("status", 0, new(VariableKind.Unsigned, 2)),
                new Variable("hp", 4, new(VariableKind.Unsigned, 2)),
                new Variable("active", 0, new(VariableKind.Bool, 1), Mask: 0x80),
            ],
            enemies.Fields);
        Assert.Equal(["stage", "enemies"], Map.Names);
    }

    [Theory]
    [InlineData("count: 0", "count and stride must be at least 1")]
    [InlineData("stride: 4", "field 'hp' runs past the entry's 4 bytes (stride)")]
    [InlineData("fields: { hp: { type: u16, address: 4 } }", "field 'hp' has an unknown option 'address'")]
    [InlineData("fields: {}", "fields must map each field's name")]
    [InlineData("length: 3", "unknown option 'length'")]
    public void Reports_bad_arrays(string change, string error)
    {
        var key = change.Split(':')[0];
        var lines = new Dictionary<string, string>
        {
            ["address"] = "address: 0x200",
            ["count"] = "count: 3",
            ["stride"] = "stride: 8",
            ["fields"] = "fields: { hp: u16 @ 4 }",
        };
        lines[key] = change;
        var yaml = "schema: 1\nsystem: { endianness: big }\nvariables:\n  enemies:\n" + string.Join("\n", lines.Values.Select(l => "    " + l));

        Assert.Contains(error, Assert.Throws<FormatException>(() => MemoryMapLoader.Parse(yaml)).Message);
    }

    [Fact]
    public void Reads_only_the_bytes_each_entry_s_fields_cover()
    {
        var plan = ReadPlan.Create(Assert.Single(Map.Arrays).Ranges, maxGap: 0);
        Assert.Equal([new AddressRange(0x200, 6), new AddressRange(0x208, 6), new AddressRange(0x210, 6)], plan.Ranges);
    }

    [Fact]
    public void Decodes_each_entry_s_fields()
    {
        var values = new MapDecoder(Map).Decode(Snapshot(Table));

        var enemies = Assert.IsType<ArrayValue>(values["enemies"]);
        Assert.Equal(3, enemies.Count);
        Assert.Equal(new Dictionary<string, object?> { ["status"] = 0x8000L, ["hp"] = 300L, ["active"] = true }, enemies[0]);
        Assert.Equal(false, enemies[1]["active"]);
        Assert.Equal(5L, enemies[2]["hp"]);
    }

    [Fact]
    public void Arrays_are_equal_when_their_entries_are()
    {
        var decoder = new MapDecoder(Map);
        var first = decoder.Decode(Snapshot(Table))["enemies"];
        var same = decoder.Decode(Snapshot([.. Table]))["enemies"];
        var hit = decoder.Decode(Snapshot([.. Table[..^1], 0x04]))["enemies"];

        Assert.Equal(first, same);
        Assert.NotEqual(first, hit);
    }

    [Fact]
    public void Paths_reach_one_field_of_one_entry()
    {
        var values = new MapDecoder(Map).Decode(Snapshot(Table));

        Assert.True(ValuePath.Parse("enemies[2].hp").TryGet(values, out var hp));
        Assert.Equal(5L, hp);
        Assert.True(ValuePath.Parse("stage").TryGet(values, out var stage));
        Assert.Equal(2L, stage);
        Assert.False(ValuePath.Parse("enemies[3].hp").TryGet(values, out _));
        Assert.False(ValuePath.Parse("enemies[0].speed").TryGet(values, out _));

        Assert.True(ValuePath.Parse("enemies[1].hp").TrySet(values, 7L));
        Assert.Equal(7L, ((ArrayValue)values["enemies"]!)[1]["hp"]);
        Assert.Equal("enemies[1].hp", ValuePath.Parse("enemies[1].hp").ToString());
    }

    [Fact]
    public void Derived_values_get_arrays_as_arrays_of_objects()
    {
        var derived = DerivedScript.Load(_dir.Write("derived.js", """
            export default {
              'enemies.active': v => v.enemies.filter(e => e.active).length,
              'enemies.weakest': v => Math.min(...v.enemies.filter(e => e.active).map(e => e.hp)),
            };
            """));
        using var evaluator = derived.CreateEvaluator(Map, DerivedScript.ReplayTickLimit);
        var values = new MapDecoder(Map).Decode(Snapshot(Table));

        Assert.Empty(evaluator.Evaluate(values));
        Assert.Equal(2L, values["enemies.active"]);
        Assert.Equal(5L, values["enemies.weakest"]);
    }

    [Fact]
    public void Scenarios_set_and_expect_entries_fields()
    {
        var derived = DerivedScript.Load(_dir.Write("derived.js", """
            export default {
              'enemies.active': v => v.enemies.filter(e => e.active).length,
            };
            """));

        Assert.Empty(DerivedScenarios.Parse("""
            start:
              enemies[0].active: true
            scenarios:
              a second enemy arrives:
                - expect: { enemies.active: 1, "enemies[1].hp": 0 }
                - set:
                    enemies[1].active: true
                    enemies[1].hp: 40
                  expect: { enemies.active: 2, "enemies[1].hp": 40 }
            """).Check(Map, derived));
        Assert.Equal(
            ["start: the map has no variable 'enemies[3].hp'"],
            DerivedScenarios.Parse("""
                start: { "enemies[3].hp": 1 }
                scenarios:
                  nothing: [{ expect: { enemies.active: 0 } }]
                """).Check(Map, derived));
    }
}
