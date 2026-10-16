using Microsoft.Extensions.FileProviders;
using Stgadgets.Core.Packs;
using Stgadgets.Core.Providers;
using Stgadgets.Providers.RetroArch;
using Stgadgets.Server.GameState;

var builder = WebApplication.CreateBuilder(args);

// Local only (docs/requirements.md).
builder.WebHost.UseUrls("http://127.0.0.1:5199");

string RepoPath(string key) => Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration[key]!));

builder.Services.AddSingleton(PackCatalog.LoadDirectory(RepoPath("Stgadgets:PacksPath")));
builder.Services.AddSingleton<IEmulatorProvider, RetroArchProvider>();
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

// Built layouts (npm run build in src/ui). During development use the Vite dev server instead.
var webRoot = RepoPath("Stgadgets:WebRoot");
if (Directory.Exists(webRoot))
{
    var files = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

app.Run();
