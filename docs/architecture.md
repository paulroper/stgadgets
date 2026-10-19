# Architecture

> Draft. Expect this to change as the first prototype teaches us things.

## Overview

stgadgets is one .NET app that runs alongside the emulator. Inside it:

```
                 ┌──────────────────────── stgadgets (.NET) ───────────────────────┐
                 │                                                                 │
 ┌──────────┐    │  ┌────────────┐  bytes  ┌────────────┐  variables ┌───────────┐ │
 │ Emulator │◀───┼─▶│  Provider  │────────▶│    Core    │───────────▶│  Server   │ │
 │(RetroArch│    │  │ (RetroArch,│         │ map loader │            │ 127.0.0.1 │ │
 │   etc.)  │    │  │  replay…)  │         │ + decoder  │            │ HTTP + WS │ │
 └──────────┘    │  └────────────┘         └────────────┘            └─────┬─────┘ │
      ▲          │                                                         │       │
      │          │  ┌─────────────────────────────────────────┐            │       │
      │ window   │  │ Overlay host (native window + webview)  │◀───────────┘       │
      └──────────┼──│ loads layout, which renders gadgets     │                    │
        bounds   │  └─────────────────────────────────────────┘                    │
                 └─────────────────────────────────────────────────────────────────┘
                                                                  │
                                              same URL also works │ as an OBS browser source
                                                                  ▼
```

| Part | Job | Knows about | Doesn't know about |
|---|---|---|---|
| **Provider** | Connects to an emulator and reads memory using the original hardware's addresses | One emulator's API | Games, gadgets |
| **Core** | Loads game packs, picks the pack for the running ROM set, asks for reads, decodes variables with its map, computes derived values | Packs: memory maps (as data) and derived values (as sandboxed JavaScript) | Emulators, UI |
| **Server** | Serves layout bundles and streams variables over WebSocket | Variables | Addresses, emulators |
| **Overlay host** | Transparent, click-through window that follows the emulator and shows a layout | Windows, the OS | Game memory |
| **Gadgets & layouts** | React components that draw variables. Game-specific layouts come from packs | Variable names | Addresses, emulators, .NET |
| **[Game packs](packs.md)** | Everything about one game: match rules, memory map, provider settings, optional UI, recordings | One game | The platform's internals |

No part of the platform contains game-specific logic: it all lives in packs ([ADR 0010](decisions/0010-game-packs.md)).

## The loop

Lag is the top requirement (see [requirements](requirements.md#latency-highest-priority)), so the loop is **driven by the provider pushing data**, with no timers in the core:

1. When a game is detected, the **core** finds its pack and works out which address ranges the pack's map needs, merges them into as few ranges as possible, and hands the provider a read plan.
2. The **provider** pushes a snapshot of those ranges: once per emulated frame where the emulator supports it, otherwise as often as it can poll ([ADR 0008](decisions/0008-providers-push-frame-snapshots.md)).
3. The **core** decodes each snapshot into variables the moment it arrives, for example `p1.score = 1530`. Then it computes the pack's [derived values](memory-maps.md#derived-values) from them, such as `fleet.speedup_in = 3`.
4. The **server** immediately sends the variables that changed to every connected layout over WebSocket.
5. The **gadgets** re-render.

The core sends every tick in order and doesn't drop or merge any, so a layout sees every value the core saw. Logic that watches for changes, such as counting shots fired, belongs in the pack's `derived.js`, not the layout. Layouts display state; they don't compute it ([ADR 0012](decisions/0012-derived-values-in-javascript.md)).

### Polling isn't frame-synced

Polling providers (RetroArch over UDP) aren't tied to the emulator's frames, so they can miss a frame or read the same frame twice. Frame-synced providers (MAME Lua) label each snapshot with its emulated frame number. For polling providers, if a game's map declares a `frame_counter` variable, the core uses that instead. Either way, each tick reports how many frames it covers (`frames: 0 | 1 | 2…`), and gadgets that care can account for missed frames.

## Process and hosting

- **One process.** The .NET app hosts the provider, the core, the server (Kestrel, listening only on `127.0.0.1`) and the overlay window. On macOS it's likely a menu bar app.
- **Layouts are web pages** served by the local server. The overlay host points a system webview at them: WKWebView on macOS, WebView2 on Windows.
- **No Node.js at runtime.** Layouts and gadgets are built ahead of time into static bundles. The webview is the JavaScript runtime.
- **OBS for free.** Because a layout is just a URL, streamers can add it as an OBS browser source instead of (or as well as) using the overlay.

See [ADR 0006](decisions/0006-react-gadgets-in-a-webview.md) and [ADR 0007](decisions/0007-native-overlay-host.md).

## Overlay host

The overlay host is **the only part of stgadgets that's specific to an OS.** Everything else (core, providers, server, SDK, gadgets) is plain cross-platform .NET or web code.

The host is a thin shell with no business logic. For each OS, it:

1. Creates a borderless, transparent, click-through window that stays on top and never takes focus.
2. Hosts the system webview (WKWebView on macOS, WebView2 on Windows) with a transparent background, pointed at the local server.
3. Tracks the emulator's window and keeps the overlay lined up with it.

.NET doesn't provide these window behaviours on any OS, so each one needs a few native calls. That's expected to be a few hundred lines per OS. macOS comes first ([ADR 0005](decisions/0005-macos-first.md)), and Windows follows in a separate session. Implementation details and pitfalls for each OS live in [ADR 0007](decisions/0007-native-overlay-host.md).

### Finding the game picture

Gadgets are often placed relative to the **game picture** (the area inside RetroArch's window that actually shows the game), not the window itself. It depends on RetroArch's aspect ratio, integer scaling and rotation settings, and none of these are available through network commands. Plan:

1. **v0:** layouts use the whole window and anchor gadgets to its edges. That's good enough for side panels.
2. **v1:** a one-off **calibration** where the user drags a rectangle over the game picture, saved per game.
3. **Later:** work it out from RetroArch's config, or detect it from the screen (needs the Screen Recording permission on macOS).

The layout receives the game picture rectangle and the game's rotation, so gadgets that draw at game coordinates (like hitboxes) can be positioned correctly.

## Repository layout

Each module has its own build, so you only need the toolchain for the part you're working on. In particular, the server builds on any OS without Xcode.

Code is organised by what it does, not what it is: see [Code conventions](conventions.md).

```
src/
  server/                            # Stgadgets.Server.slnx: any OS, plain .NET 10
    Stgadgets.Core/                  # packs, map loader & decoder, derived values (Jint) & their scenarios, recordings & expected values, provider interface
    Stgadgets.Server/                # Kestrel: game loop, WebSocket, /api/emulator, serves src/ui/dist
    providers/                       # one project per emulator
      Stgadgets.Providers.RetroArch/ # network-command provider
      (later) Stgadgets.Providers.Mame/, Stgadgets.Providers.Replay/
    (later) Stgadgets.Tools/
  host/
    lib/                             # shared by every host; plain net10.0, no OS-specific code
      Stgadgets.Host.Lib.Api/        # client for the server's HTTP API (EmulatorLocator → /api/emulator)
    macos/                           # Stgadgets.Host.Mac.slnx (includes lib/): net10.0-macos, needs Xcode
      Stgadgets.Host.Mac/            # overlay window + menu bar app
    windows/                         # (later; its solution will include lib/ too)
  ui/                                # Vite + React + TypeScript (npm)
    src/sdk/                         # connection, store, hooks, latency (@stgadgets/sdk)
    src/gadgets/                     # standard gadgets (@stgadgets/gadgets)
    src/layouts/                     # generic layout, and loading pack layouts
tests/                               # mirrors src/: tests for src/<module>/X live in tests/<module>/X.Tests
  server/
    Stgadgets.Core.Tests/            # part of the server solution; also runs packs/*/tests/derived and local recordings
  ui/                                # Vitest, run from src/ui (npm test): the app against a fake server; also runs
                                     # packs/*/tests, which import testing.ts as @stgadgets/testing
  (later) host/lib/Stgadgets.Host.Lib.Api.Tests/, server/providers/...
packs/                               # game packs: everything game-specific (docs/packs.md)
  invaders/                          # pack.yaml, map.yaml, providers/, ui/, tests/, recordings/
docs/
```

The modules only talk to each other over HTTP and WebSocket on `127.0.0.1`, never through project references: the hosts and the UI talk to the server, and the hosts load the UI from the server.

Packs are data plus optional UI code. The server loads them from `packs/` (config `Stgadgets:PacksPath`), and the UI build compiles their `ui/` folders, which may import only `@stgadgets/sdk`, `@stgadgets/gadgets` and `react`.

## Security

- The server listens only on `127.0.0.1`.
- Layouts may come from third parties, so the webview loads only from the local server, with a Content Security Policy that blocks other network access.
- Providers are read-only. stgadgets never writes to game memory.

## Open questions

- **First provider:** MAME (Lua, frame-synced) or RetroArch (network commands). Decided by the milestone 0 spike in the [roadmap](roadmap.md).
- **Overlay host spike:** confirm the overlay approach on macOS, and whether Avalonia or Photino can replace some of the native code. See [ADR 0007](decisions/0007-native-overlay-host.md).
- **Compositor cost:** whether the overlay adds lag to the emulator in any display mode ([L1](requirements.md#l1-no-added-lag-in-the-game)).
- **Getting the game picture:** whether calibration is acceptable or we need to detect it automatically.
