using System.Net;
using Stgadgets.Core.Providers;
using Stgadgets.Providers.Replay;
using Stgadgets.Providers.RetroArch;

namespace Stgadgets.Providers;

/// <summary>
/// Makes a provider, connecting to <paramref name="endpoint"/> if given, else to the emulator's default. A relative
/// path in an endpoint (such as a recording's folder) is relative to <paramref name="baseDirectory"/>.
/// </summary>
public delegate IEmulatorProvider ConnectProvider(string? endpoint, string baseDirectory);

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
        ["retroarch"] = (endpoint, _) => endpoint is null ? new RetroArchProvider() : new RetroArchProvider(IPEndPoint.Parse(endpoint)),

        // Endpoint: a recording's folder, optionally #<point> to fast-forward there and hold.
        ["replay"] = (endpoint, baseDirectory) => new ReplayProvider(
            endpoint ?? throw new ArgumentException("The replay provider needs an endpoint: a recording's folder"), baseDirectory),
    };

    /// <summary>The provider with this ID, connected to <paramref name="endpoint"/>.</summary>
    /// <param name="baseDirectory">What relative paths in the endpoint are relative to; the current directory by default.</param>
    /// <exception cref="ArgumentException">There's no provider with this ID.</exception>
    public static IEmulatorProvider Connect(string id, string? endpoint = null, string? baseDirectory = null) =>
        All.TryGetValue(id, out var connect)
            ? connect(endpoint, baseDirectory ?? Directory.GetCurrentDirectory())
            : throw new ArgumentException($"No provider '{id}'. Providers: {string.Join(", ", All.Keys)}", nameof(id));
}
