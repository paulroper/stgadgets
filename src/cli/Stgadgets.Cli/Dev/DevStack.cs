using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Stgadgets.Core.Packs;

namespace Stgadgets.Cli.Dev;

/// <summary>How the stack was started, kept in .stgadgets/dev/stack.json so it can be stopped or restarted.</summary>
internal sealed record DevOptions(string Provider, string? Endpoint, bool Overlay, int SnapshotPort)
{
    /// <summary>
    /// Options from the command line, with those not given taken from <paramref name="last"/> (a restart), or the
    /// defaults: RetroArch, no overlay, snapshots on 5198. A different provider doesn't keep the last endpoint, which
    /// was in the last provider's form.
    /// </summary>
    public static DevOptions From(DevOptions? last, string? provider, string? endpoint, bool? overlay, int? snapshotPort)
    {
        var chosen = provider ?? last?.Provider ?? "retroarch";
        return new DevOptions(
            chosen,
            endpoint ?? (chosen == last?.Provider ? last.Endpoint : null),
            overlay ?? last?.Overlay ?? false,
            snapshotPort ?? last?.SnapshotPort ?? 5198);
    }

    /// <summary>A replay held at a point: it fast-forwards there, then sends no more ticks.</summary>
    public bool Holding => Provider == "replay" && Endpoint?.Contains('#') == true;
}

/// <summary>What's running: the options, and the server's process (dotnet run).</summary>
internal sealed record DevStackState(DevOptions Options, int ServerProcessId, DateTimeOffset StartedAt);

/// <summary>The server's answer to GET /api/status.</summary>
internal sealed record ServerStatus(
    string State, string? Detail, string? Pack, long Ticks, int? PacksVersion = null,
    IReadOnlyDictionary<string, string>? DerivedErrors = null, string? Provider = null, PacksStatus? Packs = null);

/// <summary>
/// Starts and stops the parts of stgadgets for development: the server (live or replaying) and, on macOS, the overlay
/// with snapshots on. Each runs as its own process, as it would for a user, with its output in .stgadgets/dev/; this
/// only launches them and talks to them over HTTP (docs/conventions.md).
/// </summary>
internal sealed class DevStack(string repoRoot, TextWriter output)
{
    public const int ServerPort = 5199;
    private const string OverlayApp = "Stgadgets.Host.Mac.app";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web) { WriteIndented = true };

    private string Folder => Path.Combine(repoRoot, ".stgadgets", "dev");
    private string StateFile => Path.Combine(Folder, "stack.json");
    public string ServerLog => Path.Combine(Folder, "server.log");
    public string OverlayLog => Path.Combine(Folder, "overlay.log");

    /// <summary>The repository root: the nearest folder above <paramref name="start"/> with global.json and packs/.</summary>
    public static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "global.json")) && Directory.Exists(Path.Combine(dir.FullName, "packs")))
                return dir.FullName;
        }
        throw new UsageException($"{start} isn't inside the stgadgets repository");
    }

    /// <summary>The arguments for <c>dotnet</c> that run the server with these options.</summary>
    public static IReadOnlyList<string> ServerArguments(DevOptions options) =>
    [
        "run", "--project", "src/server/Stgadgets.Server", "--",
        $"--Stgadgets:Provider={options.Provider}",
        "--Stgadgets:WatchPacks=true",
        .. options.Endpoint is null ? [] : new[] { $"--Stgadgets:ProviderEndpoint={options.Endpoint}" },
    ];

    public DevStackState? Load() =>
        File.Exists(StateFile) ? JsonSerializer.Deserialize<DevStackState>(File.ReadAllText(StateFile), Json) : null;

    public async Task StartAsync(DevOptions options, bool buildUi, CancellationToken cancellationToken)
    {
        if (Load() is not null)
            throw new UsageException("The dev stack is already running: `stgadgets dev restart`, or `stgadgets dev stop` first");
        if (await StatusAsync(cancellationToken) is not null)
            throw new UsageException($"Something is already serving on port {ServerPort}: stop it first (a server started by hand?)");
        if (options.Overlay && !OperatingSystem.IsMacOS())
            throw new UsageException("--overlay needs macOS: it's the only overlay host so far");
        Directory.CreateDirectory(Folder);

        if (buildUi)
        {
            output.WriteLine("Building the UI (npm run build in src/ui)…");
            await RunToEndAsync("npm", ["run", "build"], Path.Combine(repoRoot, "src", "ui"), cancellationToken);
        }

        output.WriteLine($"Starting the server ({options.Provider}{(options.Endpoint is null ? "" : $": {options.Endpoint}")})…");
        var server = StartLogged("dotnet", ServerArguments(options), ServerLog, environment: []);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(new DevStackState(options, server.Id, DateTimeOffset.Now), Json));

        var status = await WaitForServerAsync(server, options.Holding, cancellationToken);
        output.WriteLine($"Server: {Describe(status)}. http://127.0.0.1:{ServerPort}, log {ServerLog}");

        if (options.Overlay)
        {
            output.WriteLine("Starting the overlay…");
            using var overlay = StartLogged(
                "dotnet", ["run", "--project", "src/host/macos/Stgadgets.Host.Mac"], OverlayLog,
                new() { ["STGADGETS_SNAPSHOT_PORT"] = options.SnapshotPort.ToString(CultureInfo.InvariantCulture) });
            await WaitForAsync(
                () => SnapshotAsync(options.SnapshotPort, width: 16, cancellationToken), TimeSpan.FromMinutes(3), "the overlay's snapshots",
                OverlayLog, cancellationToken);
            output.WriteLine($"Overlay: snapshots at http://127.0.0.1:{options.SnapshotPort}/snapshot, log {OverlayLog}");
        }
    }

    public async Task StopAsync()
    {
        var state = Load();
        if (state is null)
        {
            output.WriteLine("The dev stack isn't running");
            return;
        }

        TryKillTree(state.ServerProcessId);
        if (state.Options.Overlay)
        {
            foreach (var overlay in OverlayProcesses())
            {
                using (overlay)
                    overlay.Kill();
            }
        }
        File.Delete(StateFile);

        // The port is free once the server has gone.
        for (var i = 0; i < 40 && await StatusAsync(CancellationToken.None) is not null; i++)
            await Task.Delay(250);
        output.WriteLine($"Stopped the server{(state.Options.Overlay ? " and the overlay" : "")}");
    }

    public async Task WriteStatusAsync(CancellationToken cancellationToken)
    {
        var state = Load();
        var status = await StatusAsync(cancellationToken);
        if (state is null)
        {
            output.WriteLine(status is null ? "The dev stack isn't running" : $"Not started by `stgadgets dev`, but a server is running: {Describe(status)}");
            return;
        }

        var options = state.Options;
        output.WriteLine($"Started {state.StartedAt:t}: {options.Provider}{(options.Endpoint is null ? "" : $" ({options.Endpoint})")}");
        output.WriteLine($"Server: {(status is null ? $"not answering; see {ServerLog}" : Describe(status))}");
        if (options.Overlay)
        {
            var overlay = await SnapshotAsync(options.SnapshotPort, width: 16, cancellationToken) is not null;
            output.WriteLine($"Overlay: {(overlay ? $"snapshots at http://127.0.0.1:{options.SnapshotPort}/snapshot" : $"not answering; see {OverlayLog}")}");
        }
    }

    /// <summary>
    /// Saves the overlay's snapshot (a PNG of the layout as it draws it) to <paramref name="file"/>, once the server has
    /// caught up with the pack files (<see cref="WaitForPacksAsync"/>). Returns whether the overlay was visible, and
    /// anything wrong with what the snapshot shows: packs that didn't load, derived values failing.
    /// </summary>
    public async Task<(bool Visible, IReadOnlyList<string> Warnings)> SaveSnapshotAsync(string file, int? width, CancellationToken cancellationToken)
    {
        var state = Load() ?? throw new UsageException("The dev stack isn't running: `stgadgets dev start --overlay`");
        if (!state.Options.Overlay)
            throw new UsageException("The overlay isn't running: `stgadgets dev restart --overlay`");
        var warnings = Warnings(await WaitForPacksAsync(state.Options.Holding, cancellationToken)).ToList();
        var snapshot = await SnapshotAsync(state.Options.SnapshotPort, width, cancellationToken)
            ?? throw new UsageException($"The overlay isn't answering; see {OverlayLog}");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        await File.WriteAllBytesAsync(file, snapshot.Png, cancellationToken);
        return (snapshot.Visible, warnings);
    }

    /// <summary>
    /// Waits until the server shows the pack files as they are on disk: it has read them since they last changed, the
    /// game is being followed with what it read, and a held replay is back at its point. Gives up after a while, and
    /// the snapshot is taken anyway, with a warning, since an old picture is better than none.
    /// </summary>
    private async Task<ServerStatus?> WaitForPacksAsync(bool holding, CancellationToken cancellationToken)
    {
        var changedAt = PackLibrary.NewestPackFile(Path.Combine(repoRoot, "packs"));
        var clock = Stopwatch.StartNew();
        ServerStatus? last = null;
        while (clock.Elapsed < CatchUpLimit)
        {
            var status = await StatusAsync(cancellationToken);
            if (status is null)
                return null;
            if (status.Packs is { } packs && packs.ReadAt >= changedAt)
            {
                var followingLatest = status.State != "connected" || status.PacksVersion == packs.Version;
                var settled = !holding || (last?.Ticks == status.Ticks && last.PacksVersion == status.PacksVersion);
                if (packs.Error is not null || (followingLatest && settled))
                    return status;
            }
            last = status;
            await Task.Delay(500, cancellationToken);
        }
        output.WriteLine($"warning: the server hasn't caught up with the pack files after {CatchUpLimit.TotalSeconds:F0} s, so the snapshot may be out of date; " +
            "`stgadgets dev restart` reloads everything");
        return last;
    }

    private static readonly TimeSpan CatchUpLimit = TimeSpan.FromSeconds(60);

    /// <summary>What's wrong with what the server is showing: packs that didn't load, derived values failing.</summary>
    public static IEnumerable<string> Warnings(ServerStatus? status)
    {
        if (status?.Packs?.Error is { } error)
            yield return $"Pack files changed but didn't load, so the last ones are still in use: {error}";
        foreach (var (name, message) in status?.DerivedErrors ?? new Dictionary<string, string>())
            yield return $"Derived value '{name}' is failing (null until it recovers): {message}";
    }

    public static async Task<ServerStatus?> StatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Http.GetFromJsonAsync<ServerStatus>($"http://127.0.0.1:{ServerPort}/api/status", JsonSerializerOptions.Web, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static string Describe(ServerStatus status) =>
        string.Join("\n  ", Warnings(status).Prepend(status.State == "connected"
            ? $"connected, playing {status.Pack} ({status.Ticks} ticks sent)"
            : $"{status.State}{(status.Detail is null ? "" : $": {status.Detail}")}"));

    // The server is up once it answers; then give it a few seconds to find the game. A replay held at a point is ready
    // once it's there: when ticks stop coming.
    private async Task<ServerStatus> WaitForServerAsync(Process server, bool holding, CancellationToken cancellationToken)
    {
        var status = await WaitForAsync(
            async () => server.HasExited ? throw new UsageException($"The server stopped:\n{Tail(ServerLog)}") : await StatusAsync(cancellationToken),
            TimeSpan.FromMinutes(3), "the server", ServerLog, cancellationToken);
        for (var i = 0; i < 20 && !(status.State == "connected" && status.Ticks > 0); i++)
        {
            await Task.Delay(250, cancellationToken);
            status = await StatusAsync(cancellationToken) ?? status;
        }

        var clock = Stopwatch.StartNew();
        while (holding && status.State == "connected" && clock.Elapsed < TimeSpan.FromMinutes(2))
        {
            await Task.Delay(1000, cancellationToken);
            var next = await StatusAsync(cancellationToken) ?? status;
            if (next.Ticks == status.Ticks)
                break;
            status = next;
        }
        return status;
    }

    private static async Task<T> WaitForAsync<T>(Func<Task<T?>> poll, TimeSpan limit, string what, string log, CancellationToken cancellationToken)
        where T : class
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < limit)
        {
            if (await poll() is { } result)
                return result;
            await Task.Delay(500, cancellationToken);
        }
        throw new UsageException($"Gave up waiting for {what} after {limit.TotalSeconds:F0} s:\n{Tail(log)}");
    }

    private sealed record Snapshot(byte[] Png, bool Visible);

    private static async Task<Snapshot?> SnapshotAsync(int port, int? width, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync($"http://127.0.0.1:{port}/snapshot{(width is null ? "" : $"?width={width}")}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            var visible = response.Headers.TryGetValues("X-Overlay-Visible", out var values) && values.Contains("true");
            return new Snapshot(await response.Content.ReadAsByteArrayAsync(cancellationToken), visible);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // Started through the shell so its output goes straight to the log file: the process keeps running after this
    // command exits, which a pipe to this process wouldn't survive.
    private Process StartLogged(string program, IReadOnlyList<string> arguments, string log, Dictionary<string, string> environment)
    {
        var start = new ProcessStartInfo("/bin/sh") { WorkingDirectory = repoRoot, UseShellExecute = false };
        foreach (var argument in (string[])["-c", "exec \"$0\" \"$@\" > \"$STGADGETS_LOG\" 2>&1", program, .. arguments])
            start.ArgumentList.Add(argument);
        start.Environment["STGADGETS_LOG"] = log;
        foreach (var (name, value) in environment)
            start.Environment[name] = value;
        return Process.Start(start) ?? throw new IOException($"Couldn't start {program}");
    }

    private static async Task RunToEndAsync(string program, string[] arguments, string directory, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(program) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException($"Couldn't start {program}");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new UsageException($"{program} {string.Join(' ', arguments)} failed:\n{await stdout}{await stderr}");
    }

    private static void TryKillTree(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }

    // The overlay app's processes, found by path: macOS cuts process names to 16 characters ("Stgadgets.Host.M").
    private static IEnumerable<Process> OverlayProcesses() => Process.GetProcesses().Where(process =>
    {
        try
        {
            if (process.ProcessName.StartsWith("Stgadgets.Host", StringComparison.Ordinal)
                && process.MainModule?.FileName.Contains(OverlayApp, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Exited, or not ours to look at.
        }
        process.Dispose();
        return false;
    });

    private static string Tail(string log, int lines = 20) =>
        File.Exists(log) ? string.Join('\n', File.ReadLines(log).TakeLast(lines)) : "(no log)";
}
