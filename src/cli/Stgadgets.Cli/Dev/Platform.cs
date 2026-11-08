namespace Stgadgets.Cli.Dev;

/// <summary>
/// The operating systems stgadgets supports (docs/decisions/0016-macos-windows-and-linux.md), for what
/// <c>stgadgets dev</c> does differently on each. Code takes one as a parameter rather than asking the OS, so tests
/// can check every OS from any of them.
/// </summary>
internal enum Platform
{
    MacOS,
    Windows,

    Linux,

    /// <summary>Any other OS, such as FreeBSD: treated as a Unix, with no overlay host.</summary>
    Unknown,
}

internal static class Platforms
{
    /// <summary>The OS this is running on.</summary>
    public static Platform Current
    {
        get
        {
            if (OperatingSystem.IsMacOS())
                return Platform.MacOS;
            if (OperatingSystem.IsWindows())
                return Platform.Windows;
            if (OperatingSystem.IsLinux())
                return Platform.Linux;
            return Platform.Unknown;
        }
    }
}
