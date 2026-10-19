using System.Net;
using Stgadgets.Core.Providers;
using Stgadgets.Providers.RetroArch;

namespace Stgadgets.Cli.Providers;

/// <summary>Makes a provider, connecting to <c>endpoint</c> if given, else to the emulator's default.</summary>
internal delegate IEmulatorProvider ConnectProvider(string? endpoint);

/// <summary>
/// The emulators the CLI can read from, by provider ID: the same IDs as packs' providers/&lt;id&gt;.yaml files and
/// recordings' manifests. Commands that talk to an emulator take one with <c>--provider</c>.
/// </summary>
internal static class EmulatorProviders
{
    public static IReadOnlyDictionary<string, ConnectProvider> All { get; } = new Dictionary<string, ConnectProvider>
    {
        // Endpoint: host:port of RetroArch's network commands (default 127.0.0.1:55355).
        ["retroarch"] = endpoint => endpoint is null ? new RetroArchProvider() : new RetroArchProvider(IPEndPoint.Parse(endpoint)),
    };
}
