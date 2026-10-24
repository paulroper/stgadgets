using Microsoft.Extensions.FileProviders;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;
using Stgadgets.Providers;
using Stgadgets.Server.GameState;

var builder = WebApplication.CreateBuilder(args);

// Local only (docs/requirements.md).
builder.WebHost.UseUrls("http://127.0.0.1:5199");

string RepoPath(string key) => Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration[key]!));

var packsPath = RepoPath("Stgadgets:PacksPath");
// Stgadgets:WatchPacks reloads packs as their files change; `stgadgets dev` turns it on (docs/cli.md#development).
builder.Services.AddSingleton(services => new PackLibrary(
    packsPath, builder.Configuration.GetValue<bool>("Stgadgets:WatchPacks"),
    ex => services.GetRequiredService<ILogger<PackLibrary>>().LogError("Couldn't reload packs; keeping the last ones that loaded: {Message}", ex.Message)));
// The emulator to read from (docs/providers.md): Stgadgets:Provider, and optionally where it listens. Relative paths
// in the endpoint, such as a recording to replay, are relative to the folder holding packs/: the repository's root.
var providerId = builder.Configuration["Stgadgets:Provider"]
    ?? throw new InvalidOperationException($"Set Stgadgets:Provider to one of: {string.Join(", ", EmulatorProviders.All.Keys)}");
if (!EmulatorProviders.All.ContainsKey(providerId))
    throw new InvalidOperationException($"Stgadgets:Provider is '{providerId}'; it must be one of: {string.Join(", ", EmulatorProviders.All.Keys)}");
builder.Services.AddSingleton(_ =>
    EmulatorProviders.Connect(providerId, builder.Configuration["Stgadgets:ProviderEndpoint"], Path.GetDirectoryName(packsPath)));
builder.Services.AddSingleton<VariableBroadcaster>();
builder.Services.AddHostedService<GameStateService>();

var app = builder.Build();

app.UseWebSockets();
app.Map("/ws", async (HttpContext context, VariableBroadcaster broadcaster) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
        return Results.BadRequest();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await broadcaster.RunClientAsync(socket, context.RequestAborted);
    return Results.Empty;
});

// Which emulator to follow, for the overlay host (docs/providers.md).
app.MapGet("/api/emulator", (IEmulatorProvider provider) =>
    provider.Process is { } process
        ? Results.Ok(new { provider = provider.Id, process.AppName, process.ProcessId })
        : Results.NoContent());

// What the server is doing, for tools such as `stgadgets dev` (docs/cli.md): state, detail, pack, ticks sent, provider,
// and why the latest pack reload failed.
app.MapGet("/api/status", (VariableBroadcaster broadcaster, IEmulatorProvider provider, PackLibrary packs) =>
    broadcaster.Status() with { Provider = provider.Id, PackError = packs.Error });

// Built layouts (npm run build in src/ui). During development use the Vite dev server instead.
var webRoot = RepoPath("Stgadgets:WebRoot");
if (Directory.Exists(webRoot))
{
    var files = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

app.Run();
