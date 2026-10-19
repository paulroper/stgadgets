# Emulator providers

> Early interface. `IEmulatorProvider`, the RetroArch provider and the replay provider exist; the MAME provider and the rest of the interface sketched below are planned.

A provider connects stgadgets to one emulator. Its whole job is to **report what's running** and **deliver memory snapshots, using the original hardware's addresses, as close to every frame as the emulator allows**. Everything else (maps, variables, gadgets) is shared.

Providers contain no game-specific data. Anything a provider needs to know about a game, such as where a core puts its RAM, comes from the matched [game pack](packs.md#provider-settings): `packs/<id>/providers/<provider id>.yaml`.

## Choosing a provider

Every provider is listed by ID in `EmulatorProviders` (`src/lib/providers/Stgadgets.Providers`), which the server and
the CLI share. The ID is the one packs use for their settings file (`providers/<id>.yaml`): `retroarch`, and
`replay` for playing back a recording.

- **Server:** `Stgadgets:Provider` in `src/server/Stgadgets.Server/appsettings.json` (`retroarch`), and optionally
  `Stgadgets:ProviderEndpoint` for where the emulator listens, in the provider's form (RetroArch: `host:port`,
  default `127.0.0.1:55355`; replay: a recording's folder). Relative paths are relative to the repository's root
  (the folder holding `packs/`). Override either on the command line, e.g.
  `dotnet run --project src/server/Stgadgets.Server -- --Stgadgets:ProviderEndpoint=127.0.0.1:55356`. An unknown
  provider stops the server at startup, listing the known ones.
- **CLI:** `--provider` and `--endpoint` on commands that talk to an emulator, such as `recording record`
  ([cli.md](cli.md)).

Adding an emulator means writing its provider in `src/lib/providers/` and adding it to `EmulatorProviders`.

## Being a good citizen

Providers use each emulator's **official, supported interfaces**: network commands, scripting APIs, plugin systems. Not injection, and not reading another process's memory. That keeps stgadgets stable across emulator updates and avoids needing extra OS permissions. It also means we never interfere with the emulator, which is essential for [latency requirement L1](requirements.md#l1-no-added-lag-in-the-game). See [ADR 0004](decisions/0004-providers-use-official-emulator-apis.md).

## Interface

Providers **push** snapshots rather than being polled by the core (see [ADR 0008](decisions/0008-providers-push-frame-snapshots.md)). A frame-synced emulator sends one snapshot per emulated frame. A polling-only emulator runs its own polling loop inside the provider.

```csharp
// Sketch only
public interface IEmulatorProvider : IAsyncDisposable
{
    string Id { get; }                                   // "mame", "retroarch", "replay"
    ProviderCapabilities Capabilities { get; }           // FrameSynced, ReportsScreenGeometry, …

    Task ConnectAsync(CancellationToken ct);

    /// What's running, so the core can choose a memory map. Changes when the game changes.
    IAsyncEnumerable<ContentInfo?> WatchContentAsync(CancellationToken ct);

    /// Stream snapshots of the requested ranges (original hardware's addresses) until cancelled.
    IAsyncEnumerable<Snapshot> StreamAsync(ReadPlan plan, CancellationToken ct);

    /// Optional: lets the overlay host find the emulator's window.
    EmulatorProcessInfo? Process { get; }
}

public sealed record ContentInfo(
    string? Romset,                                      // "invaders"
    IReadOnlyDictionary<string, uint>? ChipCrcs,         // arcade: CRC32 of each ROM chip, by file name
    string? Sha1, string? Crc32,                         // consoles: hash of the whole ROM file
    string? System, string? Core);
public sealed record ReadPlan(IReadOnlyList<AddressRange> Ranges);
public sealed record Snapshot(long? EmulatedFrame, long ReceivedAtTicks, ReadOnlyMemory<byte> Data);
```

The code (`src/lib/Stgadgets.Core/Providers/IEmulatorProvider.cs`) currently implements a subset: `Process`, `GetContentAsync` and `StreamAsync(content, plan, settings)`, where `settings` is the matched pack's file for this provider (`ProviderSettings`, or null if the pack has none). Only the provider knows the file's shape; `settings.Read<T>()` deserializes it. Capabilities and watching for content changes get added when they're needed.

**Finding the emulator's window:** the server exposes the provider's `Process` at `GET /api/emulator` (`{ provider, appName, processId }`, or 204 when not connected). The overlay host polls it every 2 s and follows that process's window, matched by process ID, falling back to the app name. Nothing in the overlay is specific to one emulator. The RetroArch provider finds its process by name, since network commands don't say which process answered.

The core builds the `ReadPlan` from the matched pack's map, merging addresses into as few contiguous ranges as possible.

## Identifying the game

The server matches [packs](packs.md) on ROM set name, with ROM hash as a fallback (see [memory-maps.md](memory-maps.md#matching)). Providers report whatever they can:

- **ROM set name:** usually the content's file name for arcade games (`invaders.zip` → `invaders`). This breaks if the user renames files. MAME and FBNeo also sometimes use different names, and MAME occasionally renames sets between versions.
- **Chip CRCs (arcade):** survive renames and naming differences, and are what pack matching prefers (see [memory-maps.md](memory-maps.md#why-chip-crcs-not-file-hashes-for-arcade-games)).
  - *MAME:* the Lua API can list the loaded ROMs and their hashes.
  - *RetroArch:* `GET_STATUS` probably gives a CRC of the whole file, which is useless for zips. Instead, the provider finds the content's zip and reads the chip CRCs from its file listing. How we find the path (`GET_STATUS`, RetroArch's content history playlist, or the user's ROM folder) is to be decided in the spike.
- **Whole-file hashes (consoles):** SHA-1 or CRC32 of the ROM file.

## MAME provider (strong first candidate)

MAME's **Lua plugin system** is its official extension mechanism, and it suits us very well:

| Need | MAME Lua |
|---|---|
| Original hardware's addresses | `manager.machine.devices[":maincpu"].spaces["program"]:read_u8(addr)`: no translation needed |
| Frame sync | `emu.register_frame_done(fn)` runs once per emulated frame |
| What's running | `manager.machine.system.name` (ROM set), parent, ROM hashes |
| Screen geometry | Screen size and rotation are available |

The provider is two pieces: a small **Lua plugin** (shipped with stgadgets, installed into MAME's `plugins/` folder) that sends the requested ranges over a local socket every frame, and the **.NET side**, which receives them. Whether the plugin uses MAME's built-in socket support or a local file or pipe is to be decided in the spike.

## RetroArch provider

Uses RetroArch's **network command interface**: UDP on `127.0.0.1:55355`. It's **off by default**. The user enables it in `retroarch.cfg`:

```ini
network_cmd_enable = "true"
network_cmd_port = "55355"
```

Implemented in `src/lib/providers/Stgadgets.Providers.RetroArch`, used by the server and the CLI's recorder. What we learned in the spike (RetroArch 1.22.2 on macOS, FBNeo core, Space Invaders):

| Need | Command | Notes |
|---|---|---|
| What's running | `GET_STATUS` → `GET_STATUS PLAYING fb_alpha,invaders,crc32=be3e15a` | Also `PAUSED` or `CONTENTLESS`. The CRC is of the whole zip, so it's useless for matching (see [memory-maps.md](memory-maps.md#why-chip-crcs-not-file-hashes-for-arcade-games)) |
| Read memory | `READ_CORE_RAM <hex offset> <len>` → `READ_CORE_RAM <offset> <hex bytes…>` | Uses RetroAchievements addressing. **This is the one that works with FBNeo** |
| | `READ_CORE_MEMORY <hex addr> <len>` | ❌ FBNeo replies `-1 no memory map defined`. It only works with cores that publish a libretro memory map |

Measurements:

- **Round-trip ≈ one frame:** median 16.3 ms, 95th percentile 19.2 ms. This confirms RetroArch handles commands once per frame. Polling back-to-back gives about one sample per frame: in a 2-minute capture, 0.3% of intervals were over 25 ms (a missed frame) and 0.4% were under 8 ms (the same frame read twice).
- **Reply size limit:** reads of 3,000 bytes work, but 4,096 bytes get no reply. That's probably because each byte takes 3 characters of hex and macOS limits a UDP datagram to about 9 KB. The provider splits reads into chunks of at most 2,048 bytes.
- **Occasional stalls** of around 500 ms, probably while RetroArch's menu was open or its window was changing.

How the provider works:

- **Core profiles.** `READ_CORE_RAM` offsets are in the core's own layout. For FBNeo's Space Invaders, offset 0 is CPU address `0x2000` and `0x2000` bytes are readable (work RAM and video RAM). Some cores also return the bytes in a different order: FBNeo keeps 68000 RAM as little-endian words, so each pair of bytes comes back swapped, and `word_swapped: true` makes the provider swap them back. Each pack lists this for the cores it supports, keyed by RetroArch's system ID, in `providers/retroarch.yaml` ([packs.md](packs.md#provider-settings)), and `CoreProfile` reads it from the matched pack. Each new game and core pairing must be checked by hand for now.
- **Pipelined reads.** Every chunk for a tick is sent before waiting for any reply, so a tick costs one frame, not one frame per range.
- **Watching for changes.** Every 2 seconds the provider re-checks `GET_STATUS` and ends the stream if the content changed. After 10 timeouts in a row, it assumes RetroArch is gone.
- **Polling only.** It isn't synced to frames, so single-frame events can still be missed. A map's `frame_counter` variable, if it has one, can be used to spot missed or repeated frames.

## Replay provider

Plays back a **recording** instead of talking to an emulator (`src/lib/providers/Stgadgets.Providers.Replay`), so the
server, the browser, `npm run screenshot` and the overlay's snapshots work against real play with nobody playing.
It's for working on layouts and gadgets, and for bug reports ("here's the recording where the accuracy counter goes
wrong").

Its endpoint is the recording's folder:

```sh
# play the recording at its own speed, again and again
dotnet run --project src/server/Stgadgets.Server -- --Stgadgets:Provider=replay \
  --Stgadgets:ProviderEndpoint=packs/ddonpach/recordings/play-01

# fast-forward to a moment and hold there, for a still picture of it
dotnet run --project src/server/Stgadgets.Server -- --Stgadgets:Provider=replay \
  "--Stgadgets:ProviderEndpoint=packs/ddonpach/recordings/play-01#chain reaches 14"
```

- **Playing:** ticks arrive at their recorded times. At the end the stream ends, and the server starts it again from
  the first tick, with derived values starting afresh as at a new game.
- **Holding** (`#<point>`: a marker label, a tick number, `start` or `end`): every tick up to the point is played as
  fast as possible, so values that build up over time (splits, ages, the latest change) are what they were in play,
  then no more ticks come until the server stops.
- The game is the one in the recording's manifest, and the pack is matched as usual. The recording must cover every
  address the pack's map reads; one recorded into the pack without `--range` does.
- A recording has no window, so `/api/emulator` returns 204 and the overlay has nothing to follow. Look at the layout
  in a browser or with `npm run screenshot`.

Tests and tools don't use it: the core replays recordings directly for `expected.yaml` checks
([packs.md](packs.md#testing-the-map)), and the CLI searches and inspects them ([cli.md](cli.md)).

### Recording format (draft)

A recording is a directory:

```
packs/invaders/recordings/attract-01/
  manifest.json
  work_ram.bin       # one snapshot per tick, concatenated (ticks × region size)
  ticks.bin          # per tick: int64 microseconds since start, int64 emulated frame (-1 if unknown)
  expected.yaml      # in a pack: values the map must decode (docs/packs.md#testing)
```

```jsonc
{
  "schema": 1,
  "content": { "romset": "invaders", "core": "fb_alpha", "provider": "retroarch" },
  "capturedAt": "2026-10-16T19:30:00Z",
  "frameSynced": false,
  "ticks": 3600,
  "regions": [
    { "name": "work_ram", "start": "0x2000", "size": "0x400", "file": "work_ram.bin" }
  ],
  "markers": [ { "tick": 812, "label": "coin inserted" } ],
  "notes": "Attract mode, then 1 credit, played until the first life was lost."
}
```

Raw region files are simple to write, read and compare, but large: about 3.6 MB per minute for each KB recorded. Recordings are therefore local and git-ignored ([ADR 0011](decisions/0011-recordings-are-local.md)). They're made and analysed with the `stgadgets` CLI's `recording` commands ([cli.md](cli.md)), usually through the `game-recording` Claude skill (`.claude/skills/game-recording/`). Record only the ranges you need, and trim afterwards.

## Future providers

| Emulator | Likely interface |
|---|---|
| BizHawk | Its Lua or external tool API |
| FBNeo (standalone) | To be investigated |
