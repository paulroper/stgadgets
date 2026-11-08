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
    public void Runs_through_sh_on_Linux_with_the_arguments_untouched()
    {
        var start = DevStack.LoggedStartInfo("dotnet", ["run", "--", "a b", "%x%"], Platform.Linux);

        Assert.Equal("/bin/sh", start.FileName);
        Assert.Equal(["-c", "exec \"$0\" \"$@\" > \"$STGADGETS_LOG\" 2>&1", "dotnet", "run", "--", "a b", "%x%"], start.ArgumentList);
        Assert.False(start.RedirectStandardOutput);
    }

    [Fact]
    public void Runs_through_sh_on_other_Unixes_too()
    {
        Assert.Equal("/bin/sh", DevStack.LoggedStartInfo("dotnet", ["run"], Platform.MacOS).FileName);
        Assert.Equal("/bin/sh", DevStack.LoggedStartInfo("dotnet", ["run"], Platform.Unknown).FileName);
    }

    [Fact]
    public void Runs_through_cmd_on_Windows_with_each_argument_quoted()
    {
        var start = DevStack.LoggedStartInfo("dotnet", ["run", "--Stgadgets:ProviderEndpoint=packs/x/recordings/a & b#boss"], Platform.Windows);

        Assert.Equal("cmd.exe", start.FileName);
        Assert.Equal(
            "/d /s /c \"\"dotnet\" \"run\" \"--Stgadgets:ProviderEndpoint=packs/x/recordings/a & b#boss\" > \"%STGADGETS_LOG%\" 2>&1\"",
            start.Arguments);
        Assert.True(start.CreateNoWindow);
        Assert.True(start.RedirectStandardInput && start.RedirectStandardOutput && start.RedirectStandardError);
    }

    [Theory]
    [InlineData("say \"hi\"")]
    [InlineData("100%")]
    public void Refuses_arguments_cmd_would_change(string argument) =>
        Assert.Throws<UsageException>(() => DevStack.LoggedStartInfo("dotnet", ["run", argument], Platform.Windows));

    [Fact]
    public void Runs_npm_through_cmd_on_Windows_so_it_finds_npm_cmd_or_npm_exe()
    {
        var (windows, windowsArguments) = DevStack.Npm(Platform.Windows, "run", "build");
        Assert.Equal("cmd.exe", windows);
        Assert.Equal(["/d", "/c", "npm", "run", "build"], windowsArguments);

        var (macOS, macOSArguments) = DevStack.Npm(Platform.MacOS, "run", "build");
        Assert.Equal("npm", macOS);
        Assert.Equal(["run", "build"], macOSArguments);
    }

    [Fact]
    public void Runs_the_overlay_host_for_macOS_and_Windows()
    {
        Assert.Equal("src/host/macos/Stgadgets.Host.Mac", OverlayHost.For(Platform.MacOS)?.Project);
        Assert.Equal("src/host/windows/Stgadgets.Host.Windows", OverlayHost.For(Platform.Windows)?.Project);
    }

    [Fact]
    public void Has_no_overlay_host_for_other_OSes_yet()
    {
        Assert.Null(OverlayHost.For(Platform.Linux));
        Assert.Null(OverlayHost.For(Platform.Unknown));
    }

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
