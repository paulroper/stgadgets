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
/// Starts and stops the parts of stgadgets for development: the server (live or replaying) and, on an OS with an
/// overlay host (<see cref="OverlayHost.For"/>), the overlay with snapshots on. Each runs as its own process, as it would for a user, with its output in .stgadgets/dev/; this
/// only launches them and talks to them over HTTP (docs/conventions.md).
/// </summary>
internal sealed class DevStack(string repoRoot, TextWriter output)
{
    public const int ServerPort = 5199;

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
        var host = OverlayFor(options);
        Directory.CreateDirectory(Folder);

        await BuildUiAsync(buildUi, cancellationToken);

        output.WriteLine($"Starting the server ({options.Provider}{(options.Endpoint is null ? "" : $": {options.Endpoint}")})…");
        var server = StartLogged("dotnet", ServerArguments(options), ServerLog, environment: []);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(new DevStackState(options, server.Id, DateTimeOffset.Now), Json));

        var status = await WaitForServerAsync(server, options.Holding, cancellationToken);
        output.WriteLine($"Server: {Describe(status)}. http://127.0.0.1:{ServerPort}, log {ServerLog}");

        if (host is not null)
        {
            output.WriteLine($"Starting the {host.Name} overlay…");
            using var overlay = StartLogged(
                "dotnet", ["run", "--project", host.Project], OverlayLog,
                new() { ["STGADGETS_SNAPSHOT_PORT"] = options.SnapshotPort.ToString(CultureInfo.InvariantCulture) });
            await WaitForAsync(
                () => SnapshotAsync(options.SnapshotPort, width: 16, cancellationToken), TimeSpan.FromMinutes(3), "the overlay's snapshots",
                OverlayLog, cancellationToken);
            output.WriteLine($"Overlay: snapshots at http://127.0.0.1:{options.SnapshotPort}/snapshot, log {OverlayLog}");
        }
    }

    /// <summary>
    /// The overlay host these options need, or null without --overlay. Throws if there isn't one, so a restart can
    /// check before it stops what's running.
    /// </summary>
    public OverlayHost? OverlayFor(DevOptions options) => options.Overlay ? FindOverlayHost() : null;

    /// <summary>
    /// Builds the UI when asked, or when there's no build yet: without one, the server has no layouts to serve and
    /// the browser and overlay only get a 404. Installs its packages first if they aren't.
    /// </summary>
    private async Task BuildUiAsync(bool asked, CancellationToken cancellationToken)
    {
        var ui = Path.Combine(repoRoot, "src", "ui");
        var built = File.Exists(Path.Combine(ui, "dist", "index.html"));
        if (!asked && built)
            return;

        if (!Directory.Exists(Path.Combine(ui, "node_modules")))
        {
            output.WriteLine("Installing the UI's packages (npm ci in src/ui)…");
            await RunToEndAsync(Npm(Platforms.Current, "ci"), ui, cancellationToken);
        }
        output.WriteLine(built ? "Building the UI (npm run build in src/ui)…" : "There's no UI build yet: building it (npm run build in src/ui)…");
        await RunToEndAsync(Npm(Platforms.Current, "run", "build"), ui, cancellationToken);
    }

    /// <summary>
    /// How to run npm with <paramref name="arguments"/>. On Windows it's npm.cmd or npm.exe, depending on how Node
    /// was installed, and only the shell finds either by its bare name.
    /// </summary>
    public static (string Program, string[] Arguments) Npm(Platform platform, params string[] arguments) =>
        platform == Platform.Windows ? ("cmd.exe", ["/d", "/c", "npm", .. arguments]) : ("npm", arguments);

    private OverlayHost FindOverlayHost()
    {
        if (OverlayHost.For(Platforms.Current) is not { } host)
            throw new UsageException("--overlay needs macOS or Windows: there's no overlay host for this OS yet");
        if (!Directory.Exists(Path.Combine(repoRoot, host.Project)))
            throw new UsageException($"--overlay: there's no {host.Name} overlay host yet ({host.Project})");
        return host;
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
        if (state.Options.Overlay && OverlayHost.For(Platforms.Current) is { } host)
        {
            foreach (var overlay in OverlayProcesses(host))
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

    private Process StartLogged(string program, IReadOnlyList<string> arguments, string log, Dictionary<string, string> environment)
    {
        var start = LoggedStartInfo(program, arguments, Platforms.Current);
        start.WorkingDirectory = repoRoot;
        start.Environment["STGADGETS_LOG"] = log;
        foreach (var (name, value) in environment)
            start.Environment[name] = value;
        if (OperatingSystem.IsWindows())
            StandardHandles.StopInheritance();
        var process = Process.Start(start) ?? throw new IOException($"Couldn't start {program}");
        if (start.RedirectStandardOutput)
        {
            process.StandardInput.Close();
            process.StandardOutput.Close();
            process.StandardError.Close();
        }
        return process;
    }

    /// <summary>
    /// Runs <paramref name="program"/> through the OS's shell, with its output going straight to the file named by
    /// STGADGETS_LOG, so it keeps running after this command exits: a pipe to this process wouldn't survive that.
    /// </summary>
    public static ProcessStartInfo LoggedStartInfo(string program, IReadOnlyList<string> arguments, Platform platform)
    {
        if (platform != Platform.Windows)
        {
            var sh = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
            foreach (var argument in (string[])["-c", "exec \"$0\" \"$@\" > \"$STGADGETS_LOG\" 2>&1", program, .. arguments])
                sh.ArgumentList.Add(argument);
            return sh;
        }

        // cmd.exe has no way to pass arguments through untouched, as sh's "$@" does. Inside quotes it leaves & | < > alone,
        // but it still expands %VARIABLES% and can't take a quote, so arguments with those are refused. /d skips any
        // AutoRun commands; /s takes everything between the outer quotes as the command line, as it is.
        if (arguments.FirstOrDefault(argument => argument.Contains('"') || argument.Contains('%')) is { } unsafeArgument)
            throw new UsageException($"Can't pass {unsafeArgument} on Windows: cmd.exe would change it (no \" or % please)");
        var command = string.Join(' ', ((string[])[program, .. arguments]).Select(argument => $"\"{argument}\""));
        return new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/d /s /c \"{command} > \"%STGADGETS_LOG%\" 2>&1\"",
            UseShellExecute = false,
            CreateNoWindow = true,   // not tied to this command's console, so closing it doesn't stop the server
            // Not this command's own handles, which it would keep open for as long as it runs (StandardHandles).
            // StartLogged closes these at once: cmd.exe writes nothing itself, and its command's output goes to the log.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
    }

    private static async Task RunToEndAsync((string Program, string[] Arguments) command, string directory, CancellationToken cancellationToken)
    {
        var (program, arguments) = command;
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

    // The overlay host's processes, found by path: macOS cuts process names to 16 characters ("Stgadgets.Host.M").
    private static IEnumerable<Process> OverlayProcesses(OverlayHost host) => Process.GetProcesses().Where(process =>
    {
        try
        {
            if (process.ProcessName.StartsWith("Stgadgets.Host", StringComparison.Ordinal)
                && process.MainModule?.FileName.Contains(host.ExecutableMarker, StringComparison.Ordinal) == true)
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

    private static string Tail(string log, int lines = 20)
    {
        if (!File.Exists(log))
            return "(no log)";

        // The process may still be writing it, and Windows only lets a file open for writing be read if the reader allows that.
        using var reader = new StreamReader(new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        return string.Join('\n', reader.ReadToEnd().Split('\n').Select(line => line.TrimEnd('\r')).TakeLast(lines));
    }
}
