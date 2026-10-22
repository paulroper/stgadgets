# stgadgets

A platform for adding M2 ShotTriggers-style gadgets to shmups (STGs) running in emulators: score breakdowns, rank meters, chain gauges, stage splits and anything else that makes a run easier to read.

stgadgets runs alongside the emulator. It reads the game's memory through the emulator's own APIs, turns it into named variables, and draws gadgets in a transparent window over the game. The game, the emulator and the ROMs are never modified.

> **Status:** early, but playable. Packs for Space Invaders and DoDonPachi stream from RetroArch to React gadgets (DoDonPachi's include rank, chain, bees, the second loop, slowdown and stage splits), shown in the browser or in a transparent macOS overlay on top of the emulator.

## Running it

1. In `retroarch.cfg`, set `network_cmd_enable = "true"` (and turn off RetroAchievements hardcore mode). Load Space Invaders (`invaders`) or DoDonPachi (`ddonpach`, World set) with the FBNeo core.
2. Once: `cd src/ui && npm install`
3. Start stgadgets with the CLI: `bin/stgadgets dev start --build-ui`, then open http://127.0.0.1:5199. Add `--overlay` on macOS for the transparent overlay over the emulator (see below).
4. When you're done: `bin/stgadgets dev stop`

`dev start` builds the UI, starts the server (and the overlay) in the background, waits until it's up, and says whether it has found a game. `bin/stgadgets dev restart` picks up changes, and `bin/stgadgets dev status` says what's running. To play back a recording instead of the emulator: `bin/stgadgets dev start --provider replay --endpoint packs/<id>/recordings/<name>` ([providers](docs/providers.md#replay-provider)). Everything the CLI does is in [docs/cli.md](docs/cli.md).

`bin/stgadgets` builds the CLI from source when it's changed, and runs from anywhere in the repository. With [direnv](https://direnv.net) (`eval "$(direnv hook zsh)"` in `~/.zshrc`, then `direnv allow` here), `bin/` is on your PATH inside the repository, so it's just `stgadgets`.

For gadget development with hot reload, run `npm run dev` in `src/ui/` alongside the server and open the Vite URL instead. Without a server or an emulator, open `/?state=<pack>/<state>` there to see a pack's layout in one of its test game states ([developing a game pack](docs/pack-development.md#looking-at-game-states)).

Tests:

- `dotnet test src/server`: the core, and every pack's `derived.js` scenarios (`packs/<id>/tests/derived/`) and local recordings (`packs/<id>/recordings/`, git-ignored)
- `dotnet test src/cli`: the CLI
- `npm test` in `src/ui/`: the UI (`tests/ui/`) and each pack's layout tests (`packs/<id>/tests/`)

| Module | Path | Build |
|---|---|---|
| Shared libraries (core, emulator providers) | `src/lib/` | Built by the modules that use them |
| Server | `src/server/` | `dotnet build src/server` (any OS) |
| CLI: recording and pack tools ([docs/cli.md](docs/cli.md)) | `src/cli/` | `dotnet build src/cli` (any OS) |
| macOS overlay | `src/host/macos/` | `dotnet build src/host/macos` (needs Xcode) |
| UI (SDK, gadgets, layouts; builds pack UIs too) | `src/ui/` | `npm run build` |
| Game packs | `packs/` | Data, plus UI built with `src/ui` |

### macOS overlay (in progress)

For development, `bin/stgadgets dev snapshot` saves a PNG of the layout as the overlay draws it, even while it's hidden ([the build-and-look loop](docs/pack-development.md#the-build-and-look-loop)).

For fullscreen play, **turn off Game Mode for RetroArch** (menu bar game controller icon → Turn Off Game Mode). Game Mode throttles the overlay to 5 Hz, about 100–200 ms behind the game ([docs/latency.md](docs/latency.md)). Windowed play isn't affected.

The overlay host needs Xcode and the .NET macOS workload (`sudo dotnet workload install macos`). Each workload release requires one exact Xcode version: workload 27.0.10722 wants Xcode 27.0. We build with Xcode 27.1, so the host project sets `ValidateXcodeVersion=false`. Remove that once a workload supports your Xcode, or if odd build errors appear.

## A platform with three extension points

| Extension point | What it is | Written in |
|---|---|---|
| **[Game packs](docs/packs.md)** | Everything about one game: match a ROM set (`ddonpach`) to named variables (`hiscore: bcd4 @ 0x...`), plus provider settings, the game's rules as derived values, an optional layout, and tests | YAML, JavaScript for derived values (+ React for a layout) |
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
- [Gadget design](docs/gadget-design.md): how gadgets look and feel
- [Emulator providers](docs/providers.md): the provider interface, RetroArch, and the recording format
- [The stgadgets CLI](docs/cli.md): recording a game, finding addresses, and inspecting recordings
- [Roadmap](docs/roadmap.md): from Space Invaders to DoDonPachi and beyond
- [Latency measurements](docs/latency.md)
- [Code conventions](docs/conventions.md): organise code by what it does, not what it is
- [Decision records](docs/decisions/): why things are the way they are

## Targets

| | |
|---|---|
| Runtime | .NET 10 |
| Operating systems | macOS, Windows and Linux (overlay on macOS so far) |
| First emulator | RetroArch ([ADR 0009](docs/decisions/0009-first-provider-retroarch.md)); MAME next |
| Latency | No added lag in the game; overlay at most 1–2 frames behind |
| Reference games | Space Invaders (Taito, 1978), DoDonPachi (Cave, 1997) |

## Not affiliated

stgadgets is a fan project and is not affiliated with M2, Taito, Cave, or the RetroArch project. It does not include or distribute ROMs.

## License

[MIT](LICENSE)
