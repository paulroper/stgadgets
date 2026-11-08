using Stgadgets.Host.Lib.Api;

namespace Stgadgets.Host.Lib.Overlay.Tests;

public class OverlayTrackerTests
{
    private static readonly EmulatorTarget RetroArch = new("retroarch", "RetroArch", 42);
    private static readonly ScreenRect Content = new(10, 20, 640, 480);

    private readonly FakeWindows _windows = new();
    private readonly FakeOverlay _overlay = new();
    private readonly List<string> _states = [];

    private OverlayTracker Tracker(bool alwaysShow = false) => new(_windows, _overlay, alwaysShow, _states.Add);

    [Fact]
    public void FollowsTheEmulatorWhileItIsFrontmost()
    {
        _windows.Frontmost = true;
        _windows.Content = Content;

        Tracker().Track(RetroArch, serverReachable: true);

        Assert.Equal(["show 10,20 640x480"], _overlay.Calls);
        Assert.Equal(["Following RetroArch"], _states);
    }

    [Fact]
    public void HidesWhenTheEmulatorIsNotFrontmost()
    {
        _windows.Content = Content;

        Tracker().Track(RetroArch, serverReachable: true);

        Assert.Equal(["hide"], _overlay.Calls);
        Assert.Equal(["Waiting for RetroArch"], _states);
    }

    [Fact]
    public void HidesWhenTheFrontmostEmulatorHasNoWindow()
    {
        _windows.Frontmost = true;

        Tracker().Track(RetroArch, serverReachable: true);

        Assert.Equal(["hide"], _overlay.Calls);
        Assert.Equal(["No RetroArch window"], _states);
    }

    [Theory]
    [InlineData(true, "Waiting for an emulator")]
    [InlineData(false, "stgadgets server not running")]
    public void HidesWithoutAnEmulator(bool serverReachable, string state)
    {
        Tracker().Track(null, serverReachable);

        Assert.Equal(["hide"], _overlay.Calls);
        Assert.Equal([state], _states);
    }

    [Fact]
    public void TellsTheWindowAndTheUserOnlyAboutChanges()
    {
        var tracker = Tracker();
        _windows.Frontmost = true;
        _windows.Content = Content;

        tracker.Track(RetroArch, serverReachable: true);
        tracker.Track(RetroArch, serverReachable: true);
        _windows.Content = Content with { Width = 800 };
        tracker.Track(RetroArch, serverReachable: true);
        _windows.Frontmost = false;
        tracker.Track(RetroArch, serverReachable: true);
        tracker.Track(RetroArch, serverReachable: true);

        Assert.Equal(["show 10,20 640x480", "show 10,20 800x480", "hide"], _overlay.Calls);
        Assert.Equal(["Following RetroArch", "Waiting for RetroArch"], _states);
    }

    [Fact]
    public void AppliesTheSameFrameAgainAfterForgetting()
    {
        var tracker = Tracker();
        tracker.Track(null, serverReachable: true);

        tracker.Forget();
        tracker.Track(null, serverReachable: true);

        Assert.Equal(["hide", "hide"], _overlay.Calls);
    }

    [Fact]
    public void AlwaysShowUsesThePlaceholderWithoutAnEmulatorWindow()
    {
        var tracker = Tracker(alwaysShow: true);

        tracker.Track(null, serverReachable: true);
        tracker.Track(RetroArch, serverReachable: true);
        _windows.Content = Content;
        tracker.Track(RetroArch, serverReachable: true);

        Assert.Equal(["show 100,100 400x600", "show 10,20 640x480"], _overlay.Calls);
        Assert.Equal(["Waiting for an emulator", "Following RetroArch"], _states);
    }

    private sealed class FakeWindows : IEmulatorWindows
    {
        public bool Frontmost { get; set; }
        public ScreenRect? Content { get; set; }

        public ScreenRect? FindContentRect(EmulatorTarget target) => Content;

        public bool IsFrontmost(EmulatorTarget target) => Frontmost;
    }

    private sealed class FakeOverlay : IOverlayWindow
    {
        public List<string> Calls { get; } = [];

        public void ShowAt(ScreenRect frame) => Calls.Add($"show {frame.X},{frame.Y} {frame.Width}x{frame.Height}");

        public void Hide() => Calls.Add("hide");
    }
}
