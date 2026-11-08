using Stgadgets.Host.Lib.Overlay;

namespace Stgadgets.Host.Windows;

internal static class Program
{
    // Windows spike experiments, on top of always-show (OverlaySettings): not-layered. See docs/latency.md.
    internal static readonly OverlaySettings Settings = OverlaySettings.FromEnvironment();

    [STAThread]   // WinForms and WebView2 need a single-threaded apartment
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new OverlayApp(Settings));
    }
}
