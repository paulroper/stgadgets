using Stgadgets.Core.Maps;
using Stgadgets.Core.Recordings;

namespace Stgadgets.Core.Tests;

/// <summary>Checks maps/invaders.yaml against a real capture (tests/fixtures/recordings/invaders/play-01).</summary>
public class InvadersRecordingTests
{
    private static readonly MapDecoder Decoder = new(MemoryMapLoader.LoadFile(Path.Combine(RepoPaths.Maps, "invaders.yaml")));
    private static readonly Recording Recording = Recording.Load(RepoPaths.Recording("invaders", "play-01"));
    private static readonly List<Dictionary<string, object?>> Ticks = Recording.Snapshots.Select(Decoder.Decode).ToList();

    /// <summary>The distinct values a variable takes over the recording, in order.</summary>
    private static List<object?> History(string name)
    {
        var history = new List<object?>();
        foreach (var tick in Ticks)
        {
            if (history.Count == 0 || !Equals(history[^1], tick[name]))
                history.Add(tick[name]);
        }
        return history;
    }

    private static int Marker(string label) => Recording.Manifest.Markers!.Single(m => m.Label.StartsWith(label)).Tick;

    [Fact]
    public void Starts_mid_wave()
    {
        var first = Ticks[0];
        Assert.Equal(720L, first["p1.score"]);
        Assert.Equal(15L, first["aliens_remaining"]);
        Assert.Equal(2L, first["p1.ships_reserve"]);
        Assert.Equal(0L, first["hiscore"]);
        Assert.Equal(1L, first["credits"]);
    }

    [Fact]
    public void Score_follows_kills_and_the_mystery_ship()
    {
        Assert.Equal<object?>([720L, 820L, 840L, 860L, 890L, 920L, 930L, 940L, 960L], History("p1.score"));

        var ufo = Marker("mystery ship");
        Assert.Equal(720L, Ticks[ufo - 1]["p1.score"]);
        Assert.Equal(820L, Ticks[ufo]["p1.score"]);
        Assert.Equal(Ticks[ufo - 1]["aliens_remaining"], Ticks[ufo]["aliens_remaining"]);   // not an alien
    }

    [Fact]
    public void Aliens_count_down_one_per_kill() =>
        Assert.Equal<object?>([15L, 14L, 13L, 12L, 11L, 10L, 9L, 8L], History("aliens_remaining"));

    [Fact]
    public void Spare_ships_drop_after_each_death_except_the_last() =>
        Assert.Equal<object?>([2L, 1L, 0L], History("p1.ships_reserve"));

    [Fact]
    public void Player_state_is_ff_while_alive_and_flickers_while_exploding()
    {
        var hit = Marker("player hit (1st");
        Assert.Equal(0xFFL, Ticks[hit - 1]["player.state"]);
        Assert.Contains(Ticks[hit]["player.state"], new object?[] { 0L, 1L });
    }

    [Fact]
    public void Hiscore_updates_at_game_over()
    {
        var gameOver = Marker("game over");
        Assert.Equal(0L, Ticks[gameOver - 1]["hiscore"]);
        Assert.Equal(960L, Ticks[gameOver]["hiscore"]);
    }
}
