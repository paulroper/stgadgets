# Gadgets & layouts

> Draft API. The SDK (`src/ui/src/sdk`), a few gadgets, the generic layout and pack layouts exist; the rest is planned.

Gadgets are React components. Layouts are pages that arrange gadgets for a game. The platform's own gadgets and the generic layout live in `src/ui`. A game's own layout and gadgets live in its [game pack](packs.md#ui). Both are written in TypeScript, built with Vite into static bundles, and served by stgadgets to the overlay's webview, or to OBS, or to a normal browser while you develop.

## Concepts

| | What | Example |
|---|---|---|
| **Gadget** | A component that draws one or more variables | `<Score player={1} />`, `<RankMeter />` |
| **Layout** | A page that arranges gadgets, for one game (in its pack) or for any game | `packs/invaders/ui/layout.tsx`, `GenericLayout` |
| **SDK** | `@stgadgets/sdk`: the connection to stgadgets, hooks, and layout helpers | `useVar('p1.score')` |
| **Standard gadgets** | `@stgadgets/gadgets`: the platform's gadgets, for any layout | `<Score name="p1.score" label="1UP" />` |

## Reading variables

Variables come from the active [memory map](memory-maps.md). The SDK holds them in a store and re-renders components when the variables they use change.

```tsx
import { useVar } from '@stgadgets/sdk';

export function Score({ player = 1 }: { player?: 1 | 2 }) {
  const score = useVar<number>(`p${player}.score`);
  if (score === undefined) return null;          // not in this game's map
  return <div className="score">{String(score).padStart(4, '0')}</div>;
}
```

Hooks:

| Hook | Returns |
|---|---|
| `useVar<T>(name)` | The current value, or `undefined` if the map doesn't define it |
| `useGame()` | `{ title, manufacturer, year, romset, screen: { width, height, rotation } }`, or `null` when nothing is running |
| `usePack()` | The matched pack's ID, e.g. `'invaders'`, or `null` |
| `useConnection()` | `{ state, detail }`; `state` is `'connected' \| 'no-emulator' \| 'no-content' \| 'no-pack' \| 'disconnected'` and a few more |
| `useOnFrame(fn)` | Calls `fn(vars, prev, info)` for **every** tick, in order. This is the building block for trackers |
| `useGameRect()` | Where the game picture sits in the overlay, plus helpers to convert game coordinates into overlay coordinates |

### Globals, for code that isn't React

The same data is available as a plain global, for quick experiments or other frameworks:

```js
window.stgadgets.vars['p1.score'];          // current value
window.stgadgets.subscribe((vars, prev) => { … });
```

Globals don't trigger React re-renders, so React gadgets should use the hooks.

## Trackers

Many of the most interesting ShotTriggers-style stats aren't stored in memory. They're worked out by watching variables over time: shots fired, accuracy, time per stage, score per stage, when lives were lost.

Trackers live in the SDK as ordinary hooks built on `useOnFrame`, so gadget authors never need to touch .NET:

```tsx
// Count rising edges: e.g. player-shot-active going false → true
export function useRisingEdges(name: string) {
  const [count, setCount] = useState(0);
  useOnFrame((vars, prev) => {
    if (vars[name] && !prev[name]) setCount(c => c + 1);
  });
  return count;
}
```

The core sends every tick in order, so trackers don't miss changes because React rendered late. They can still miss things that happened between two polls. See [architecture](architecture.md#polling-isnt-frame-synced).

If a tracker turns out to be expensive or widely shared, it can move into the core later without changing its hook API.

## Layouts

Today a layout is a React component. When the server matches a [game pack](packs.md), the UI shows the pack's `ui/layout.tsx` (its default export), or the **generic layout** if the pack has none. The generic layout (`src/ui/src/layouts/GenericLayout.tsx`) shows standard gadgets for the [standard variable names](memory-maps.md#standard-variable-names). Gadgets render nothing for variables the map doesn't define.

Pack layouts are built with the UI: Vite finds every `packs/*/ui/layout.tsx` and builds each into its own lazily loaded chunk. Pack UI code may import only `@stgadgets/sdk`, `@stgadgets/gadgets`, `react` and its own files, and the build fails otherwise ([packs.md](packs.md#what-pack-uis-may-import)), so packs can be built and distributed separately later.

Planned layout helpers, as a sketch:

```tsx
import { defineLayout, Panel, GameSpace } from '@stgadgets/sdk/react';
import { Score, Lives, Accuracy } from '@stgadgets/gadgets';

export default defineLayout({
  render: () => (
    <>
      <Panel anchor="left">
        <Score player={1} />
        <Lives player={1} />
      </Panel>
      <Panel anchor="right">
        <Accuracy />
      </Panel>
      <GameSpace>{/* children use game coordinates, rotated & scaled to fit */}</GameSpace>
    </>
  ),
});
```

- **`Panel`** sticks to an edge of the game picture. This suits the black bars beside a vertical game.
- **`GameSpace`** is a container that uses the game's own coordinates, with rotation and scaling applied. It's for things drawn over the action, such as hitboxes or markers.
- **Requirements:** gadgets can declare which variables they need. A generic layout hides any gadget whose variables the current map doesn't provide.

## Design

Gadgets follow one design language: a consistent look built from shared parts and tokens, icons before words, no game assets, and small touches of fun. Layouts use the platform's grid of panels beside the game. See [gadget design](gadget-design.md).

## Keeping gadgets fast

Gadgets are part of the latency budget (see [requirements](requirements.md#l2-the-overlay-must-keep-up-with-the-game)). Each tick should reach the screen at the webview's next paint.

- **Show new values immediately.** No debouncing or tweening that makes a value appear late. Decorative animation is fine as long as it starts from the new value.
- **Keep render work small.** Re-render only the gadgets whose variables changed (`useVar` handles this).
- **Skip React for high-rate gadgets.** Anything updating every frame with many elements (bullet counts, hitboxes, graphs) should draw on a `<canvas>` or update the DOM through refs inside `useOnFrame`, not through React state.
- **Watch the latency debug gadget** while developing. It shows the time from provider to paint for every tick.

## Input display

An input display can't come from the emulator APIs, because none of them report controller input. It will need either the game's input bytes in RAM (through the map, so it works anywhere) or stgadgets reading the controller directly. To be decided when we build it.

## Development workflow

1. Run stgadgets with the **replay provider** pointed at a recording (see [providers.md](providers.md#replay-provider)). You don't need an emulator.
2. Run `npm run dev` in `src/ui/`. The Vite dev server proxies the WebSocket to stgadgets.
3. Open the layout in a browser and edit gadgets with hot reload.

## Wire protocol (internal)

The SDK handles this, but for reference:

```jsonc
// server → client, on connect and whenever the game changes
{ "type": "hello", "pack": "invaders",
  "game": { "title": "Space Invaders", "manufacturer": "Taito", "year": 1978, "romset": "invaders", "screen": { … } },
  "variables": { "p1.score": { "type": "bcd2" }, … }, "values": { "p1.score": 0, … } }

// server → client, every tick (changed values only)
{ "type": "tick", "seq": 18234, "frames": 1, "changed": { "p1.score": 150 } }

// server → client
{ "type": "status", "state": "no-emulator" }
```

## Open questions

- **Packaging third-party packs:** separately built pack UIs loaded at runtime as ES modules, with the SDK, gadgets and React shared through an import map. Needs its own decision on SDK versioning and trust ([ADR 0010](decisions/0010-game-packs.md)).
- **Configuration:** user-tweakable settings for each layout, such as colours, which gadgets to show, and scale.
