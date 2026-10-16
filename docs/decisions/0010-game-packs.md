# 0010. Everything game-specific lives in game packs

- **Status:** Accepted; recordings amended by [0011](0011-recordings-are-local.md): they're local and git-ignored
- **Date:** 2026-10-16

## Context

After the Space Invaders spike, game-specific knowledge is spread through the platform:

- `maps/invaders.yaml`: the memory map
- `CoreProfile` in the RetroArch provider: a hardcoded `fb_alpha/invaders → RAM at 0x2000`
- `src/ui/src/layouts/InvadersClassic.tsx`, which `main.tsx` always renders
- `InvadersRecordingTests` (C#) and `tests/fixtures/recordings/invaders/`

stgadgets is meant to be a platform. Adding a game should never mean changing platform code, and eventually other people should be able to write and distribute support for games.

## Decision

All game-specific material moves into a **game pack**: one folder per game under `packs/`. The platform (server, providers, hosts and UI) contains **no game-specific logic**.

### Identity

A pack is named after the game's **MAME parent ROM set** (`packs/ddonpach/`). Clones, regional versions and bootlegs are listed in the pack's match rules instead of getting their own packs. Clones whose addresses differ can override parts of the map later (`extends`-style, as already planned in [memory-maps.md](../memory-maps.md)).

### Contents

```
packs/<parent-romset>/
  pack.yaml                  # id, title, manufacturer, year, screen, match rules (romsets, program CRCs)
  map.yaml                   # memory map: system, regions, variables
  providers/
    retroarch.yaml           # per-core address translation, e.g. fb_alpha: RAM base 0x2000
  ui/
    layout.tsx               # default export: the pack's layout (optional)
    gadgets/                 # the pack's own React components (optional)
  recordings/
    <name>/                  # recording format from providers.md, plus:
      expected.yaml          # values the map must decode from this recording
```

- **The map** follows the existing format, minus the `game`/`match` sections, which move to `pack.yaml`.
- **Provider settings** are keyed by provider and core. Providers read them from the pack that matched, so they have no hardcoded game data.
- **The UI** is optional. A pack without one gets the platform's **generic layout**, built from standard gadgets that bind to the [standard variable names](../memory-maps.md#standard-variable-names).

### Runtime

1. The provider reports what's running, as today.
2. The server matches it to a pack. A pack's match rules replace today's per-map matching, with the same priority order (program CRCs, then ROM set name).
3. The server loads the pack's map and gives the provider the pack's settings for it.
4. The `hello` message includes the pack ID. The UI loads that pack's layout, or the generic layout.

### Building pack UIs: bundled for now, separable later

For now, pack UIs are **built together with the UI**. Vite discovers `packs/*/ui/layout.tsx` and compiles each into its own lazily loaded chunk, so packs share one React and are type-checked with the platform.

So that packs can later be built and distributed separately, a pack's UI code may import only:

- `@stgadgets/sdk`: connection, hooks (`useVar`, `useOnFrame`, …), layout helpers
- `@stgadgets/gadgets`: the platform's standard gadgets
- `react`
- its own files

It must never import from `src/ui` by relative path. The UI build enforces this through path aliases (and a lint rule if needed). Later, separately built packs would load at runtime as ES modules, with the SDK, gadgets and React shared through an import map. That step is out of scope here.

### Testing: the platform provides the harness, packs provide the data

Each recording in a pack carries an `expected.yaml`:

```yaml
at:                                  # values at a tick number or at a marker label from manifest.json
  start:                             # "start" = tick 0
    p1.score: 720
  "game over, hiscore 0 -> 960":
    hiscore: 960
sequences:                           # every distinct value, in order, over the whole recording
  p1.ships_reserve: [2, 1, 0]
```

The platform provides:

- a **harness** that replays a recording through its pack's map and checks the expected values;
- a generic test that runs the harness for every pack and every recording, so `dotnet test src/server` covers all bundled packs;
- later, a CLI command (`stgadgets pack test <dir>`) for third-party pack authors.

Testing pack UIs (Vitest against recordings through a replay client) can come later, once a pack's UI is substantial enough to need it.

## Consequences

- Adding a game means adding a folder: map, settings, optional UI and recordings, with **no platform code and no C#**.
- The platform tests stop containing game knowledge. `InvadersRecordingTests` is replaced by the generic harness plus `packs/invaders/recordings/play-01/expected.yaml`.
- `maps/` and `tests/fixtures/recordings/` go away. The `Stgadgets.Core` map loader becomes a pack loader. The RetroArch `CoreProfile` table becomes data read from packs.
- The import rule is the price of keeping packs separable later. It needs enforcing from day one, or packs will quietly come to depend on the platform's internals.
- Separately distributed packs (runtime loading, SDK versioning, trust and sandboxing for third-party JavaScript) are a later decision, and will get their own ADR.

## Implementation plan

1. Create `packs/invaders/` from `maps/invaders.yaml`, the `fb_alpha/invaders` core profile, `InvadersClassic.tsx` and `tests/fixtures/recordings/invaders/play-01` (writing `expected.yaml` from the current `InvadersRecordingTests` checks).
2. Core: pack loading (`pack.yaml` + `map.yaml` + provider settings), and pack matching in place of map matching.
3. RetroArch provider: read core profiles from the matched pack's `providers/retroarch.yaml`, and remove the hardcoded table.
4. Server: load packs from `packs/` (config `Stgadgets:PacksPath`), and add `pack` to the `hello` message.
5. UI: `@stgadgets/sdk` and `@stgadgets/gadgets` aliases, discovery of pack layouts, a generic fallback layout, and selection by the pack ID in `hello`. `main.tsx` stops importing any game's layout.
6. Tests: the expected-values harness, plus the generic test over all packs. Delete `InvadersRecordingTests`.
7. Docs: add `docs/packs.md` (pack author guide), and update `memory-maps.md`, `gadgets.md`, `providers.md`, `architecture.md` (repository layout), the README and `CLAUDE.md` (rule: no game-specific logic in the platform).
