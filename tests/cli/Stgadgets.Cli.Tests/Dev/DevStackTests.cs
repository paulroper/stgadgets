using Stgadgets.Cli.Dev;
using Stgadgets.Core.Packs;

namespace Stgadgets.Cli.Tests.Dev;

/// <summary>
/// The parts of <c>stgadgets dev</c> that don't start processes. Starting and stopping the real server and overlay is
/// checked by hand (docs/cli.md#development).
/// </summary>
public sealed class DevStackTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Runs_the_server_with_the_provider_and_its_endpoint() =>
        Assert.Equal(
            ["run", "--project", "src/server/Stgadgets.Server", "--", "--Stgadgets:Provider=replay", "--Stgadgets:WatchPacks=true", "--Stgadgets:ProviderEndpoint=packs/x/recordings/y#boss"],
            DevStack.ServerArguments(new DevOptions("replay", "packs/x/recordings/y#boss", Overlay: false, SnapshotPort: 5198)));

    [Fact]
    public void Leaves_the_endpoint_to_the_provider_when_there_isnt_one() =>
        Assert.Equal(
            ["run", "--project", "src/server/Stgadgets.Server", "--", "--Stgadgets:Provider=retroarch", "--Stgadgets:WatchPacks=true"],
            DevStack.ServerArguments(new DevOptions("retroarch", null, Overlay: true, SnapshotPort: 5198)));

    [Fact]
    public void Defaults_to_retroarch_without_the_overlay() =>
        Assert.Equal(new DevOptions("retroarch", null, false, 5198), DevOptions.From(last: null, null, null, null, null));

    [Fact]
    public void A_restart_keeps_what_isnt_given()
    {
        var last = new DevOptions("replay", "packs/x/recordings/y", Overlay: true, SnapshotPort: 6000);

        Assert.Equal(last, DevOptions.From(last, null, null, null, null));
        Assert.Equal(last with { Endpoint = "packs/x/recordings/y#boss" }, DevOptions.From(last, null, "packs/x/recordings/y#boss", null, null));
        Assert.Equal(last with { Overlay = false }, DevOptions.From(last, null, null, overlay: false, null));
    }

    [Fact]
    public void A_restart_with_another_provider_drops_the_last_endpoint() =>
        Assert.Equal(
            new DevOptions("retroarch", null, true, 6000),
            DevOptions.From(new DevOptions("replay", "packs/x/recordings/y", true, 6000), "retroarch", null, null, null));

    [Fact]
    public void Finds_the_repository_root_from_inside_it()
    {
        _dir.Write("repo/global.json", "{}");
        Directory.CreateDirectory(Path.Combine(_dir.Path, "repo", "packs"));
        Directory.CreateDirectory(Path.Combine(_dir.Path, "repo", "src", "ui"));

        Assert.Equal(Path.Combine(_dir.Path, "repo"), DevStack.FindRepoRoot(Path.Combine(_dir.Path, "repo", "src", "ui")));
    }

    [Fact]
    public void Says_when_its_not_in_the_repository() =>
        Assert.Throws<UsageException>(() => DevStack.FindRepoRoot(_dir.Path));

    [Fact]
    public void Start_leaves_replays_to_recording_play() =>
        Assert.Contains(
            StgadgetsCommand.Create().Parse(["dev", "start", "--provider", "replay"]).Errors,
            e => e.Message.Contains("'replay' not recognized", StringComparison.Ordinal));

    [Fact]
    public void Warns_about_packs_that_didnt_load_and_failing_derived_values() =>
        Assert.Equal(
            [
                "Pack files changed but didn't load, so the last ones are still in use: bad YAML",
                "Derived value 'rank' is failing (null until it recovers): v.x is undefined",
            ],
            DevStack.Warnings(new ServerStatus("connected", null, "test", 10, 1, new Dictionary<string, string> { ["rank"] = "v.x is undefined" })
            {
                Packs = new PacksStatus(DateTimeOffset.UnixEpoch, 1, "bad YAML"),
            }));

    [Fact]
    public void A_replay_held_at_a_point_is_holding() =>
        Assert.Equal(
            [true, false, false],
            [
                new DevOptions("replay", "packs/x/recordings/y#boss", false, 5198).Holding,
                new DevOptions("replay", "packs/x/recordings/y", false, 5198).Holding,
                new DevOptions("retroarch", null, false, 5198).Holding,
            ]);
}
