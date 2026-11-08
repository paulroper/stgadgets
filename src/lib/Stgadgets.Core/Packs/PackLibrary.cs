namespace Stgadgets.Core.Packs;

/// <summary>
/// Where the packs stand, for tools that need the server to have caught up with the files on disk: when they were last
/// read, how many times they've loaded, and why the latest read failed, if it did.
/// </summary>
public sealed record PacksStatus(DateTimeOffset ReadAt, int Version, string? Error);

/// <summary>
/// The installed packs, optionally reloaded whenever a pack's own files change: pack.yaml, map.yaml, derived.js or
/// providers/*.yaml. A pack's recordings, tests and UI don't change what the server reads, so they're ignored. A reload
/// that fails keeps the packs from before, and <see cref="Error"/> says why until one succeeds.
/// </summary>
public sealed class PackLibrary : IDisposable
{
    // Editors save in several writes (truncate, write, rename), so reload once they've stopped.
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(200);

    private readonly string _path;
    private readonly Action<Exception>? _reloadFailed;
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _settle;
    private readonly Lock _gate = new();
    private CancellationTokenSource _changed = new();
    private DateTimeOffset _readAt;

    /// <param name="watch">Reload when pack files change; for development, where they're being edited.</param>
    /// <param name="reloadFailed">Called when a reload fails, to log it.</param>
    public PackLibrary(string path, bool watch, Action<Exception>? reloadFailed = null)
    {
        _path = Path.GetFullPath(path);
        _reloadFailed = reloadFailed;
        _readAt = DateTimeOffset.UtcNow;
        Catalog = PackCatalog.LoadDirectory(_path);
        _settle = new Timer(_ => Reload());
        if (!watch)
            return;

        _watcher = new FileSystemWatcher(_path) { IncludeSubdirectories = true };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    public PackCatalog Catalog { get; private set; }

    /// <summary>Counts the loads that worked, so the packs in use can be told apart from the latest.</summary>
    public int Version { get; private set; } = 1;

    /// <summary>Why the latest reload failed, or null when the packs are as on disk.</summary>
    public string? Error { get; private set; }

    /// <summary>The packs and their <see cref="Version"/>, read together.</summary>
    public (PackCatalog Catalog, int Version) Current
    {
        get
        {
            lock (_gate)
                return (Catalog, Version);
        }
    }

    public PacksStatus Status()
    {
        lock (_gate)
            return new PacksStatus(_readAt, Version, Error);
    }

    /// <summary>Cancelled when the packs are reloaded. Take it before reading <see cref="Catalog"/>, so a reload in between isn't missed.</summary>
    public CancellationToken Changed
    {
        get
        {
            lock (_gate)
                return _changed.Token;
        }
    }

    /// <summary>Whether <paramref name="path"/>, relative to the packs folder, is one of the files a pack is loaded from.</summary>
    public static bool IsPackFile(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) switch
        {
            [_, "pack.yaml" or "map.yaml" or "derived.js"] => true,
            [_, "providers", var file] => file.EndsWith(".yaml", StringComparison.Ordinal),
            _ => false,
        };

    /// <summary>When the newest of the files packs are loaded from (<see cref="IsPackFile"/>) was last written.</summary>
    public static DateTimeOffset NewestPackFile(string packs) =>
        Directory.EnumerateDirectories(packs)
            .SelectMany(pack => ((string[])["pack.yaml", "map.yaml", "derived.js"]).Select(name => Path.Combine(pack, name))
                .Concat(Directory.Exists(Path.Combine(pack, "providers")) ? Directory.EnumerateFiles(Path.Combine(pack, "providers"), "*.yaml") : []))
            .Where(File.Exists)
            .Select(file => new DateTimeOffset(File.GetLastWriteTimeUtc(file)))
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();

    /// <summary>Loads the packs again; when it works, swaps them in and cancels <see cref="Changed"/>.</summary>
    public void Reload()
    {
        // Taken before reading, so a file written during the read counts as newer and is read again after it.
        var readAt = DateTimeOffset.UtcNow;
        PackCatalog catalog;
        try
        {
            catalog = PackCatalog.LoadDirectory(_path);
        }
        // A pack being edited can be broken in any way: bad YAML, a derived.js syntax error, a file half-written.
        catch (Exception ex)
        {
            lock (_gate)
                (_readAt, Error) = (readAt, ex.Message);
            _reloadFailed?.Invoke(ex);
            return;
        }

        CancellationTokenSource changed;
        lock (_gate)
        {
            Catalog = catalog;
            Version++;
            _readAt = readAt;
            Error = null;
            changed = _changed;
            _changed = new CancellationTokenSource();
        }
        // Not disposed: the game loop may still be linking to its token. Without a timer it holds nothing to free.
        changed.Cancel();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _settle.Dispose();
        _changed.Dispose();
    }

    // Names, not full paths: on macOS events come with symlinks resolved (/private/var for /var), so a full path may not
    // start with the folder being watched.
    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (IsPackFile(e.Name ?? "") || (e is RenamedEventArgs { OldName: { } oldName } && IsPackFile(oldName)))
            _settle.Change(Settle, Timeout.InfiniteTimeSpan);
    }
}
