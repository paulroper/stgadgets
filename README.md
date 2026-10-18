# stgadgets

A platform for adding M2 ShotTriggers-style gadgets to shmups (STGs) running in emulators: score breakdowns, rank meters, chain gauges, input displays and anything else that makes a run easier to read.

stgadgets runs alongside the emulator. It reads the game's memory through the emulator's own APIs, turns it into named variables, and draws gadgets in a transparent window over the game. The game, the emulator and the ROMs are never modified.

> **Status:** early spike. Space Invaders' score streams from RetroArch to React gadgets, shown in the browser or in a transparent macOS overlay on top of the emulator.

## Running it

1. In `retroarch.cfg`, set `network_cmd_enable = "true"` (and turn off RetroAchievements hardcore mode). Load Space Invaders (`invaders`) with the FBNeo core.
2. Build the UI: `cd src/ui && npm install && npm run build`
3. Run the server: `dotnet run --project src/server/Stgadgets.Server`, then open http://127.0.0.1:5199
4. Optionally, run the macOS overlay: `dotnet run --project src/host/macos/Stgadgets.Host.Mac` (see below)

For gadget development with hot reload, run `npm run dev` in `src/ui/` alongside the server and open the Vite URL instead.

Tests: `dotnet test src/server`, and `npm test` in `src/ui/` for the UI (tests in `tests/ui/`). Each pack's map is also checked against any local recordings in `packs/<id>/recordings/`; these are git-ignored and made with the `game-recording` Claude skill.

| Module | Path | Build |
|---|---|---|
| Server (core, providers, server, tests) | `src/server/` | `dotnet build src/server` (any OS) |
| macOS overlay | `src/host/macos/` | `dotnet build src/host/macos` (needs Xcode) |
| UI (SDK, gadgets, layouts; builds pack UIs too) | `src/ui/` | `npm run build` |
| Game packs | `packs/` | Data, plus UI built with `src/ui` |

### macOS overlay (in progress)

**Snapshots for development:** start the overlay with `STGADGETS_SNAPSHOT_PORT=5198` and `GET http://127.0.0.1:5198/snapshot` returns a PNG of the layout as the overlay draws it, at the emulator window's size, on a transparent background (the game isn't included). It works while the overlay is hidden too, by drawing it invisibly for half a second; the `X-Overlay-Visible` header says which. Off unless the variable is set.

For fullscreen play, **turn off Game Mode for RetroArch** (menu bar game controller icon → Turn Off Game Mode). Game Mode throttles the overlay to 5 Hz, about 100–200 ms behind the game ([docs/latency.md](docs/latency.md)). Windowed play isn't affected.

The overlay host needs Xcode and the .NET macOS workload (`sudo dotnet workload install macos`). Each workload release requires one exact Xcode version: workload 27.0.10722 wants Xcode 27.0. We build with Xcode 27.1, so the host project sets `ValidateXcodeVersion=false`. Remove that once a workload supports your Xcode, or if odd build errors appear.

## A platform with three extension points

| Extension point | What it is | Written in |
|---|---|---|
| **[Game packs](docs/packs.md)** | Everything about one game: match a ROM set (`ddonpach`) to named variables (`hiscore: bcd4 @ 0x...`), plus provider settings, an optional layout and test recordings | YAML (+ React for a layout) |
| **[Gadgets & layouts](docs/gadgets.md)** | UI components that draw those variables | React + TypeScript |
| **[Emulator providers](docs/providers.md)** | Connect to an emulator and read memory | C# / .NET |

Adding a game only needs a pack folder: no platform code and no C#. Adding a gadget should only need some React. Adding an emulator should only need a provider. None of them should need changes to the others, and the platform contains no game-specific logic.

```
 Emulator ──official API──▶ Provider ──bytes──▶ Core ──variables──▶ Gadgets (React)
                                              (memory map)          in a transparent overlay
```

See [Architecture](docs/architecture.md) for the full picture.

## Docs

- [Requirements](docs/requirements.md): latency first. STG players feel every frame
- [Architecture](docs/architecture.md): the parts of the system and how they fit together
- [Game packs](docs/packs.md): what goes in a pack
- [Developing a game pack](docs/pack-development.md): the workflow, from research to gadgets
- [Memory maps](docs/memory-maps.md): the per-game map format
- [Gadgets & layouts](docs/gadgets.md): the React SDK
- [Emulator providers](docs/providers.md): the provider interface, RetroArch, and recordings for replay
- [Roadmap](docs/roadmap.md): from Space Invaders to DoDonPachi
- [Latency measurements](docs/latency.md)
- [Code conventions](docs/conventions.md): organise code by what it does, not what it is
- [Decision records](docs/decisions/): why things are the way they are

## Targets

| | |
|---|---|
| Runtime | .NET 10 |
| First OS | macOS (Windows to follow) |
| First emulator | RetroArch ([ADR 0009](docs/decisions/0009-first-provider-retroarch.md)); MAME next |
| Latency | No added lag in the game; overlay at most 1–2 frames behind |
| First game | Space Invaders (Taito, 1978) |
| Dream game | DoDonPachi (Cave, 1997) |

## Not affiliated

stgadgets is a fan project and is not affiliated with M2, Taito, Cave, or the RetroArch project. It does not include or distribute ROMs.
