using System.Net;
using Stgadgets.Core.Providers;
using Stgadgets.Providers.RetroArch;

namespace Stgadgets.Providers;

/// <summary>Makes a provider, connecting to <c>endpoint</c> if given, else to the emulator's default.</summary>
public delegate IEmulatorProvider ConnectProvider(string? endpoint);

/// <summary>
/// Every emulator stgadgets can read from, by provider ID: the IDs of packs' providers/&lt;id&gt;.yaml files and of
/// recordings' manifests. The server picks one from its configuration and the CLI from <c>--provider</c>, so adding
/// an emulator means adding its provider here (docs/providers.md).
/// </summary>
public static class EmulatorProviders
{
    public static IReadOnlyDictionary<string, ConnectProvider> All { get; } = new Dictionary<string, ConnectProvider>
    {
        // Endpoint: host:port of RetroArch's network commands (default 127.0.0.1:55355).
        ["retroarch"] = endpoint => endpoint is null ? new RetroArchProvider() : new RetroArchProvider(IPEndPoint.Parse(endpoint)),
    };

    /// <summary>The provider with this ID, connected to <paramref name="endpoint"/>.</summary>
    /// <exception cref="ArgumentException">There's no provider with this ID.</exception>
    public static IEmulatorProvider Connect(string id, string? endpoint = null) =>
        All.TryGetValue(id, out var connect)
            ? connect(endpoint)
            : throw new ArgumentException($"No provider '{id}'. Providers: {string.Join(", ", All.Keys)}", nameof(id));
}
