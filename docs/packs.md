# Game packs

Everything stgadgets knows about a game lives in its **game pack**: one folder under `packs/`. Adding a game means adding a folder, with no platform code and no C#. The platform (server, providers, hosts and UI) contains no game-specific logic. See [ADR 0010](decisions/0010-game-packs.md) for why.

The first pack is [`packs/invaders/`](../packs/invaders/).

## What's in a pack

```
packs/<parent-romset>/
  pack.yaml                  # id, title, manufacturer, year, screen, match rules
  map.yaml                   # memory map: system, regions, variables
  derived.js                 # values computed from the map's variables each tick (optional)
  providers/
    retroarch.yaml           # settings for one provider, e.g. per-core address translation
  ui/
    layout.tsx               # default export: the pack's layout (optional)
    gadgets/                 # the pack's own React components (optional)
  tests/
    states.ts                # named game states for testing the UI without an emulator (optional)
    *.test.tsx               # the UI's tests (optional)
  recordings/
    <name>/                  # a recording (docs/providers.md#recording-format-draft), plus:
      expected.yaml          # values the map (and derived.js) must produce from it
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

Set `word_swapped: true` when the core returns each 16-bit word byte-swapped, as FBNeo does for 68000 games such as DoDonPachi. The provider then reads whole words and swaps them back, so maps keep the original hardware's byte order.

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

Never import from `src/ui` by relative path. The UI build fails if a pack does. A pack's tests may also import
`vitest`, `@testing-library/react` and `@stgadgets/testing` ([testing the UI](#testing-the-ui)).

For now, pack UIs are built together with the platform's UI: `npm run build` in `src/ui` finds every `packs/*/ui/layout.tsx`, type-checks it, and builds it into its own chunk that loads on first use. `npm run dev` hot-reloads pack UIs too.

## Testing

### Testing the map

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

- Values are numbers (decimal or `0x` hex), `true`, `false` or `null` (bytes that don't decode, such as bad BCD). [Derived values](memory-maps.md#derived-values) can also be fractions or strings.
- Derived values are checked like any other variable. A derived value that fails on any tick is reported too.
- To check a value just before an event, use the marker's tick minus one.
- A recording without `expected.yaml` is still checked: every tick must decode, so the map's variables have to fall inside the recorded regions.

`dotnet test src/server` replays every local recording of every pack through the pack's map and reports each mismatch (with no recordings present, there's nothing to check and it passes), for example `at 'player hit (1st death)' (tick 587): player.state is 0, expected 1`. A `stgadgets pack test <dir>` command for pack authors outside this repository is planned.

### Testing the UI

A pack's UI is tested against **game states**: named sets of values, as the server would send them on one tick, so
the overlay can be shown at any moment of the game (a boss fight, the continue screen, the second loop) without a
server or an emulator. They live in `tests/states.ts`, and are committed:

```ts
import type { GameStates } from "@stgadgets/testing";

const saucerFlying = {
  "p1.score": 820,
  "saucer.status": "flying",
  // ... every variable the layout reads, map and derived alike
} satisfies GameStates[string];

export default {
  saucerFlying,
  /** One invader left. */
  lastInvader: { ...saucerFlying, "fleet.aliens": 1, "saucer.status": "none" },
} satisfies GameStates;
```

- A state lists every variable the layout reads. A variable that's missing is one the map doesn't define, and its
  gadget renders nothing; use `null` for a value the game hasn't set.
- Start from a real moment: take one tick's values from a recording, then derive the other states from it by
  changing what that moment changes. Keep each state believable: derived values that follow from each other should
  agree (rank's parts add up to `rank.model`, and so on).

Tests in `tests/*.test.tsx` show the pack's whole layout in a state and check what's on screen, using
`@stgadgets/testing`:

```tsx
import { showState, textOf } from "@stgadgets/testing";
import { expect, it } from "vitest";
import states from "./states";

it("shows the march at full speed with one invader left", async () => {
  await showState("invaders", states.lastInvader);

  expect(textOf("SPEED-UP IN")).toBe("MAX");
});
```

- `showState(pack, values)` starts the real UI against a fake server, matches the pack and waits for its layout. It
  returns the fake server, whose `tick(changed)` sends further ticks.
- `gadget(label)` finds a gadget by its label (the whole label, or a pattern such as `/^BOSS/`); `textOf(label,
  part)` is the text of its `.value`, or of another part such as `".note"`.

To look at a state rather than test it, screenshot it with `npm run screenshot -- --state <pack>/<state>`
([looking at game states](pack-development.md#looking-at-game-states)).

Every state of every pack is also checked to render without React errors. Run the tests with `npm test` in
`src/ui`. For now, pack tests run with the platform's tests from `src/ui`; `@stgadgets/testing` will become a
package that pack authors install once packs can live outside this repository.

## Adding a game

The steps below are the checklist. [Developing a game pack](pack-development.md) covers the workflow behind them:
where to find addresses, attribution and licensing, choosing gadgets, and the build-and-look loop.

1. Create `packs/<parent romset>/` with `pack.yaml` and `map.yaml`. Start from published addresses where they exist ([finding addresses](memory-maps.md#finding-addresses)).
2. Add provider settings for the emulators and cores you've checked, e.g. `providers/retroarch.yaml`.
3. Record a session with markers at the events you care about, find or confirm addresses, and write its `expected.yaml`. The `game-recording` skill walks through this.
4. Optionally, add `derived.js` for values that follow from memory plus knowledge of the game, so the layout only has to display them.
5. Optionally, add `ui/layout.tsx`. Until then the generic layout is used.
6. If you added a UI, add game states and tests for it in `tests/` ([testing the UI](#testing-the-ui)).
7. Run `dotnet test src/server` and, if you added a UI, `npm run build` and `npm test` in `src/ui`.
