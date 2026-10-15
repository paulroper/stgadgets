# Emulator providers

> Draft interface. Nothing is implemented yet.

A provider connects stgadgets to one emulator. Its whole job is to **report what's running** and **deliver memory snapshots, using the original hardware's addresses, as close to every frame as the emulator allows**. Everything else (maps, variables, gadgets) is shared.

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

The core builds the `ReadPlan` from the active map, merging addresses into as few contiguous ranges as possible.

## Identifying the game

The core matches maps on ROM set name, with ROM hash as a fallback (see [memory-maps.md](memory-maps.md#matching)). Providers report whatever they can:

- **ROM set name:** usually the content's file name for arcade games (`invaders.zip` → `invaders`). This breaks if the user renames files. MAME and FBNeo also sometimes use different names, and MAME occasionally renames sets between versions.
- **Chip CRCs (arcade):** survive renames and naming differences, and are what map matching prefers (see [memory-maps.md](memory-maps.md#why-chip-crcs-not-file-hashes-for-arcade-games)).
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

| Need | Command |
|---|---|
| What's running | `GET_STATUS` → state, system, content name, CRC32 |
| Read memory | `READ_CORE_MEMORY <hex addr> <len>` → `READ_CORE_MEMORY <addr> <hex bytes…>` or an error |

Limitations:

- **Polling only.** It isn't synced to frames, so single-frame events can be missed. The provider polls faster than the frame rate and uses a map's `frame_counter` variable, if it has one, to spot missed or repeated frames.
- **Handled once per frame (probably).** If RetroArch handles commands once per frame, each round-trip can cost up to a frame. The provider sends all reads for a tick at once without waiting for replies, and never waits on one reply before sending the next request.
- **Needs a core memory map.** `READ_CORE_MEMORY` goes through the core's memory map (the same one RetroAchievements uses). Cores without one can't be read, and arcade cores may expose RAM at their own addresses. A small **core profile** for each core and system translates the original hardware's addresses into the core's.
- **Reply sizes** are probably limited, so large regions need several requests.

> ⚠️ Whether RetroArch's arcade cores on macOS expose usable memory is **unchecked**. It's the first task in the [roadmap](roadmap.md).

## Replay provider

Plays back a **recording** instead of talking to an emulator. It's used for:

- **Tests:** fixed input for checking map decoding and trackers
- **Gadget development:** work on layouts without running an emulator
- **Map authoring:** RAM search works on recordings (see [memory-maps.md](memory-maps.md#finding-addresses))
- **Bug reports:** "here's the recording where the accuracy counter goes wrong"

Playback follows the original timestamps by default, and can also step through frame by frame.

### Recording format (draft)

A recording is a directory:

```
tests/fixtures/recordings/invaders/attract-01/
  manifest.json
  work_ram.bin       # one snapshot per tick, concatenated (ticks × region size)
  ticks.bin          # per tick: int64 microseconds since start, int64 emulated frame (-1 if unknown)
```

```jsonc
{
  "schema": 1,
  "content": { "romset": "invaders", "provider": "mame", "emulatorVersion": "…" },
  "capturedAt": "2026-10-16T19:30:00Z",
  "frameSynced": true,
  "ticks": 3600,
  "regions": [
    { "name": "work_ram", "start": "0x2000", "size": "0x400", "file": "work_ram.bin" }
  ],
  "markers": [ { "tick": 812, "label": "coin inserted" } ],
  "notes": "Attract mode, then 1 credit, played until the first life was lost."
}
```

Raw region files are simple to write, read and compare. Space Invaders' 1 KB of work RAM comes to about 3.6 MB per minute, which is fine to keep in git. Larger systems will need compression, Git LFS, or recording only the ranges a map uses. We'll decide that when we get there.

## Future providers

| Emulator | Likely interface |
|---|---|
| BizHawk | Its Lua or external tool API |
| FBNeo (standalone) | To be investigated |
