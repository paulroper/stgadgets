namespace Stgadgets.Cli.Dev;

/// <summary>An overlay host that <c>stgadgets dev --overlay</c> can run (src/host).</summary>
/// <param name="Name">The OS it's for, for messages.</param>
/// <param name="Project">Its project, relative to the repository root, for <c>dotnet run</c>.</param>
/// <param name="ExecutableMarker">Part of its executable's path, to find its processes by when stopping it.</param>
internal sealed record OverlayHost(string Name, string Project, string ExecutableMarker)
{
    /// <summary>The overlay host for <paramref name="platform"/>, or null if it has none yet.</summary>
    public static OverlayHost? For(Platform platform) => platform switch
    {
        Platform.MacOS => new("macOS", "src/host/macos/Stgadgets.Host.Mac", "Stgadgets.Host.Mac.app"),
        Platform.Windows => new("Windows", "src/host/windows/Stgadgets.Host.Windows", "Stgadgets.Host.Windows.exe"),
        _ => null,
    };
}
