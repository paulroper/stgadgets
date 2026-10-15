# stgadgets

A platform for adding M2 ShotTriggers-style gadgets to shmups (STGs) running in emulators: score breakdowns, rank meters, chain gauges, input displays and anything else that makes a run easier to read.

stgadgets runs alongside the emulator. It reads the game's memory through the emulator's own APIs, turns it into named variables, and draws gadgets in a transparent window over the game. The game, the emulator and the ROMs are never modified.

> **Status:** design. There is no code yet, only the docs below.

## A platform with three extension points

| Extension point | What it is | Written in |
|---|---|---|
| **[Memory maps](docs/memory-maps.md)** | Match a ROM set (`ddonpach`) to named variables (`hiscore: bcd4 @ 0x...`) | YAML |
| **[Gadgets & layouts](docs/gadgets.md)** | UI components that draw those variables | React + TypeScript |
| **[Emulator providers](docs/providers.md)** | Connect to an emulator and read memory | C# / .NET |

Adding a game should only need a map file. Adding a gadget should only need some React. Adding an emulator should only need a provider. None of them should need changes to the others.

```
 Emulator ──official API──▶ Provider ──bytes──▶ Core ──variables──▶ Gadgets (React)
                                              (memory map)          in a transparent overlay
```

See [Architecture](docs/architecture.md) for the full picture.

## Docs

- [Requirements](docs/requirements.md): latency first. STG players feel every frame
- [Architecture](docs/architecture.md): the parts of the system and how they fit together
- [Memory maps](docs/memory-maps.md): the per-game map format
- [Gadgets & layouts](docs/gadgets.md): the React SDK
- [Emulator providers](docs/providers.md): the provider interface, RetroArch, and recordings for replay
- [Roadmap](docs/roadmap.md): from Space Invaders to DoDonPachi
- [Decision records](docs/decisions/): why things are the way they are

## Targets

| | |
|---|---|
| Runtime | .NET 10 |
| First OS | macOS (Windows to follow) |
| First emulator | RetroArch or MAME (decided by a spike) |
| Latency | No added lag in the game; overlay at most 1–2 frames behind |
| First game | Space Invaders (Taito, 1978) |
| Dream game | DoDonPachi (Cave, 1997) |

## Not affiliated

stgadgets is a fan project and is not affiliated with M2, Taito, Cave, or the RetroArch project. It does not include or distribute ROMs.
