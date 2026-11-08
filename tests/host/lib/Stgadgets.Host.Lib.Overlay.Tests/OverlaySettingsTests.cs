namespace Stgadgets.Host.Lib.Overlay.Tests;

public class OverlaySettingsTests
{
    [Fact]
    public void DefaultsWhenNothingIsSet()
    {
        var settings = OverlaySettings.From(_ => null);

        Assert.Equal(new Uri("http://127.0.0.1:5199/"), settings.Server);
        Assert.Equal(new Uri("http://127.0.0.1:5199/?overlay"), settings.LayoutUrl);
        Assert.Null(settings.SnapshotPort);
        Assert.Empty(settings.Experiments);
        Assert.False(settings.AlwaysShow);
    }

    [Fact]
    public void ReadsTheVariables()
    {
        var variables = new Dictionary<string, string>
        {
            ["STGADGETS_SERVER"] = "http://127.0.0.1:6000/",
            ["STGADGETS_SNAPSHOT_PORT"] = "5198",
            ["STGADGETS_EXPERIMENT"] = "always-show, keyable,",
        };

        var settings = OverlaySettings.From(name => variables.GetValueOrDefault(name));

        Assert.Equal(new Uri("http://127.0.0.1:6000/?overlay"), settings.LayoutUrl);
        Assert.Equal(5198, settings.SnapshotPort);
        Assert.True(settings.AlwaysShow);
        Assert.True(settings.Experiment("keyable"));
        Assert.False(settings.Experiment("normal-level"));
    }

    [Fact]
    public void IgnoresASnapshotPortThatIsNotANumber()
    {
        var settings = OverlaySettings.From(name => name == "STGADGETS_SNAPSHOT_PORT" ? "on" : null);

        Assert.Null(settings.SnapshotPort);
    }
}
