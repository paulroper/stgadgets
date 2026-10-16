# Game packs

Everything stgadgets knows about a game lives in its **game pack**: one folder under `packs/`. Adding a game means adding a folder, with no platform code and no C#. The platform (server, providers, hosts and UI) contains no game-specific logic. See [ADR 0010](decisions/0010-game-packs.md) for why.

The first pack is [`packs/invaders/`](../packs/invaders/).

## What's in a pack

```
packs/<parent-romset>/
  pack.yaml                  # id, title, manufacturer, year, screen, match rules
  map.yaml                   # memory map: system, regions, variables
  providers/
    retroarch.yaml           # settings for one provider, e.g. per-core address translation
  ui/
    layout.tsx               # default export: the pack's layout (optional)
    gadgets/                 # the pack's own React components (optional)
  recordings/
    <name>/                  # a recording (docs/providers.md#recording-format-draft), plus:
      expected.yaml          # values the map must decode from it
```

Only `pack.yaml` and `map.yaml` are required.

## Naming

A pack is named after the game's **MAME parent ROM set**: `packs/ddonpach/`, not `packs/dodonpachi/` or `packs/ddonpachj/`. Clones, regional versions and bootlegs don't get their own packs. They're listed in the parent pack's match rules instead. The pack's `id` must match its folder name.

## pack.yaml

```yaml
schema: 1

id: invaders                    # the MAME parent ROM set; must match the folder name
title: Space Invaders
manufacturer: Taito
year: 1978
screen: { width: 256, height: 224, rotation: 270 }   # native raster; MAME lists ROT270

match:
  romsets: [invaders]
  program_crcs:                 # 8080 program ROMs (MAME 0.261 set)
    invaders.h: 0x734f5ad8
    invaders.g: 0x6bfaca4a
    invaders.f: 0x0ccead96
    invaders.e: 0x14e538b0
```

`match` decides when the pack applies: program ROM CRCs first, then ROM set names. The server uses the first pack that matches. See [memory maps → matching](memory-maps.md#matching).

## map.yaml

The game's variables, in the original hardware's address space. The format is in [memory-maps.md](memory-maps.md).

## Provider settings

Some providers need game-specific settings. Each provider reads its own file, `providers/<provider id>.yaml`, from the matched pack, and nothing else in the platform looks at it.

**RetroArch** (`providers/retroarch.yaml`): `READ_CORE_RAM` offsets are in each core's own layout, so the provider needs to know where offset 0 lands in the original hardware's address space, and how many bytes the core exposes from there. Keys under `cores` are RetroArch's system IDs for the core, as `GET_STATUS` reports them:

```yaml
schema: 1
cores:
  fb_alpha:                     # FBNeo
    ram_base: 0x2000
    ram_size: 0x2000
```

Without an entry for the running core, the provider reports that the core isn't supported yet. Each core needs checking by hand for now (see [providers.md](providers.md#retroarch-provider)).

## UI

`ui/layout.tsx` default-exports a React component: the pack's layout. When the server matches the pack, the UI loads it. A pack without a UI gets the platform's **generic layout**, which shows the standard gadgets for the [standard variable names](memory-maps.md#standard-variable-names) the map defines.

```tsx
import { Counter, LatencyMeter, Score } from "@stgadgets/gadgets";

export default function InvadersClassic() {
  return (
    <div className="panel">
      <Score name="p1.score" label="SCORE<1>" />
      <Score name="hiscore" label="HI-SCORE" />
      <Counter name="aliens_remaining" label="INVADERS" />
      <LatencyMeter />
    </div>
  );
}
```

### What pack UIs may import

So that packs can later be built and distributed separately, pack UI code may import only:

- `@stgadgets/sdk`: the connection and hooks (`useVar`, `useOnFrame`, …). See [gadgets.md](gadgets.md)
- `@stgadgets/gadgets`: the platform's standard gadgets
- `react`
- its own files

Never import from `src/ui` by relative path. The UI build fails if a pack does.

For now, pack UIs are built together with the platform's UI: `npm run build` in `src/ui` finds every `packs/*/ui/layout.tsx`, type-checks it, and builds it into its own chunk that loads on first use. `npm run dev` hot-reloads pack UIs too.

## Testing

A pack's map is proven with recordings. They're **local and git-ignored** ([ADR 0011](decisions/0011-recordings-are-local.md)): make them with the `game-recording` Claude skill (`.claude/skills/game-recording/`) while onboarding or debugging a game. Each folder under `recordings/` is a recording in the [recording format](providers.md#recording-format-draft), with an `expected.yaml` beside it listing values the map must decode:

```yaml
at:                                         # values at one point in the recording
  start:                                    # "start" is tick 0
    p1.score: 720
  586:                                      # a tick number
    player.state: 0xFF
  "game over, hiscore 0 -> 960":           # a marker label from manifest.json
    hiscore: 960

sequences:                                  # every distinct value, in order, over the whole recording
  p1.ships_reserve: [2, 1, 0]
```

- Values are numbers (decimal or `0x` hex), `true`, `false` or `null` (bytes that don't decode, such as bad BCD).
- To check a value just before an event, use the marker's tick minus one.
- A recording without `expected.yaml` is still checked: every tick must decode, so the map's variables have to fall inside the recorded regions.

`dotnet test src/server` replays every local recording of every pack through the pack's map and reports each mismatch (with no recordings present, there's nothing to check and it passes), for example `at 'player hit (1st death)' (tick 587): player.state is 0, expected 1`. A `stgadgets pack test <dir>` command for pack authors outside this repository is planned.

## Adding a game

1. Create `packs/<parent romset>/` with `pack.yaml` and `map.yaml`. Start from published addresses where they exist ([finding addresses](memory-maps.md#finding-addresses)).
2. Add provider settings for the emulators and cores you've checked, e.g. `providers/retroarch.yaml`.
3. Record a session with markers at the events you care about, find or confirm addresses, and write its `expected.yaml`. The `game-recording` skill walks through this.
4. Optionally, add `ui/layout.tsx`. Until then the generic layout is used.
5. Run `dotnet test src/server` and, if you added a UI, `npm run build` in `src/ui`.
