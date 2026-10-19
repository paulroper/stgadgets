# Gadget design language

How gadgets should look and feel ([ADR 0013](decisions/0013-no-game-assets-in-gadgets.md), [ADR 0014](decisions/0014-gadget-styling-and-layout.md)). This applies to the standard gadgets in `src/ui` and to the gadgets that ship in packs. [Gadgets & layouts](gadgets.md) covers the API, and [Keeping gadgets fast](gadgets.md#keeping-gadgets-fast) still comes first: nothing here is worth a frame of lag.

Four principles:

1. **Consistent.** Every out-of-the-box gadget looks like part of one set, whichever game it's for.
2. **Icons before words.** Show a thing as a picture when a picture reads faster than a label. Draw it ourselves, with emoji or SVG. Never use the game's own assets.
3. **Fun.** Small touches that reflect the game give a layout its character: the mystery ship that hovers while the real one is on screen in Space Invaders, the bee beside DoDonPachi's bee count.
4. **Steady.** Flicker is the enemy. A gadget reserves all the space it will ever need up front, so nothing on screen jumps around and pulls the player's eye from the game.

## 1. Consistent

Gadgets share one look, set by the platform's stylesheet (`src/ui/src/styles.css`). A pack's gadgets build on it rather than restyling it.

- **Panels:** dark, translucent (`--panel`), a thin light border, rounded corners.
- **Type:** one monospace font. A label is small, spaced capitals in `--label`, e.g. `CHAIN`. A value is large tabular figures in `--text`. Secondary text is `--dim`.
- **Missing values** show `-` and keep the gadget's shape. A gadget whose variable isn't in the map renders nothing.
- **Colour means the same thing everywhere:**

  | Token | Meaning | Examples |
  |---|---|---|
  | `--live` (yellow) | Live, gaining, the current one | Score gain popup, live chain, current split |
  | `--good` (green) | Good, met, safe | Stage clear, second-loop condition met, rank going down |
  | `--bad` (red) | Danger, arriving, bad | Boss here, heavy slowdown, rank going up |
  | `--orange`, `--blue`, `--purple`, `--pink` | Categories with no good or bad meaning | Power types, the parts of rank |

### Gadget anatomy

Every gadget is built from the same parts, all defined by the platform:

| Class | What it is |
|---|---|
| `.gadget` | The gadget. A grid that spaces its parts evenly (`--gadget-gap`) |
| `.label` | Its name, e.g. `CHAIN`. It sits right on top of the value |
| `.value` (`.small`) | The number or state |
| `.unit` | What the value counts, beside it: `/ 13`, `HIT`, `s` |
| `.bar` > `.bar-fill` | A progress bar. The gadget colours the fill, e.g. `.chain .bar-fill { background: var(--live) }`. A bar can hold several fills side by side, as rank does |
| `.pips` | A row of pips. The gadget draws its own pip shape: bees, power levels, shots |
| `.note` | A line of detail underneath: `best 52`, `next +100` |

A gadget's own classes are prefixed with its name (`.bees-stage`, `.rank-key`) and live in the gadget's CSS file next to it.

### Styling rules

- **Plain CSS and custom properties.** No CSS framework. Pack UIs will one day be built separately and loaded at runtime, and variables cross that boundary with no build step: a third-party pack just writes `var(--good)`.
- **Size everything in `rem`**, so it scales with the layout. Only hairlines (1px borders and outlines) use `px`. Use `em` only for something that should follow the text it sits in, like the score gain popup.
- **Use tokens, never raw values.** Colours, sizes, spacing, corner radii and animation timing are variables in `styles.css`. If a gadget needs a value no token covers, add a token rather than a literal. Tints of a token use `color-mix()`, e.g. `color-mix(in srgb, var(--live) 40%, transparent)`.
- **The platform sets the layout's look for every pack.** Text sizes, spacing and side widths are global tokens, so every layout matches. A pack overrides one only when its game really needs it, by setting the token on its layout's root element. It doesn't override `.panel` or `.gadget` rules.
- **Packs win.** The platform's styles are in `@layer platform`. A pack's CSS is unlayered, so it always beats the platform's, whatever order the files load in.
- **Style for one game goes in that pack**, scoped to its gadget's class. If two packs want the same thing, it belongs in the platform.

## Layout

A layout puts panels of gadgets beside the game picture and never over it. Vertical games leave black bars on both sides, and that's where gadgets go. (Drawing over the action, such as hitboxes, is a different job, for the planned `GameSpace` in [gadgets.md](gadgets.md#layouts).)

```tsx
<div className="layout">                      {/* left side | game picture | right side */}
  <div className="layout-left">
    <div className="panel">…</div>            {/* panels stack from the top */}
  </div>
  <div className="layout-right">
    <div className="panel">…</div>
    <div className="layout-bottom">           {/* optional: a group at the bottom of the screen */}
      <div className="panel">…</div>
    </div>
  </div>
</div>
```

- **The page is a grid:** `left game right`. The game's column takes the space that's left and stays empty.
- **Panels on one side share its width**, so their edges line up. Each side is at least `--left-min-width` or `--right-min-width` wide (about 17rem), and grows if a gadget needs more.
- **The layout scales with the window's height**, which is what sizes a vertical game's picture. The root font size is `clamp(12px, 1.75vh, 24px)`, so 1rem is 16px in a 917px-tall window, and everything sized in rem follows. Check other sizes with `npm run screenshot` ([pack development](pack-development.md#checking-other-window-sizes)).
- **Group gadgets into panels by topic:** scoring in one, the game's systems (rank, chain, bees) in another, timing and debug in a third.
- **Put what you read most at the top**, and timing and debug gadgets at the bottom.

**Grid or flexbox?** Use grid for two-dimensional structure: the page, and anything with rows and columns that must line up. Use flexbox for a single run of things, such as a stack of panels, a row of pips, or an icon beside its label. Each is simpler in its own case.

## 2. Icons before words

A glance during play should be enough. Pictures are faster to read than labels, and they make the overlay feel like part of the game.

- **Use an icon when it's unambiguous:** lives as a row of ships, bees as 🐝, a boss as a marker on a progress bar. Numbers stay as numbers.
- **Keep a label when an icon would leave you guessing.** `RANK` or `CHAIN` as a word beats an abstract symbol. An icon beside a label is fine, as with `🐝 BEES`.
- **Every icon needs an accessible name:** `aria-label` on SVG, or `aria-hidden` on decorative emoji next to a text label.

### Where icons come from

| Source | Use for | Notes |
|---|---|---|
| **Emoji** | Quick, decorative touches: 🐝, ⭐, 💣 | Free, and in colour. They look different on each OS, so don't rely on an exact shape or colour. |
| **SVG drawn in code** | Icons whose shape or colour matters, or that need to animate | Colour them with CSS `fill`, so they follow the palette. The Space Invaders pack's `Sprite` component draws pixel art from rows of `#` and `.`, crisp at any size. |

### No real game assets

Don't ship sprites, screenshots, fonts, logos or sound taken from a game's ROM or its artwork. We draw everything ourselves.

- **Draw our own version, not a copy.** Our own drawings of a game's ships or enemies are fine, as long as they aren't pixel-for-pixel copies of the originals: change the proportions, detail or size so it's recognisably ours. Anything extracted from the game or its artwork is never fine.
- **Why:** game assets are copyrighted, and stgadgets and its packs are meant to be shared freely. Our own drawings also match each other and the palette, so they help with (1).

Real assets could become an extension point later: for example, a pack could describe sprites, and stgadgets could take them from the user's own ROM or emulator at runtime, so they're never distributed. That needs its own decision record.

## 3. Fun

Gadgets should feel like they belong to the game and enjoy it. A few small touches do more than a lot of decoration.

- **React to what's happening.** The best touches follow game state: the mystery ship hovers and glows only while the real one is flying, the bee pips fill up, `PERFECT!` appears when a stage is perfect, the boss bar turns red when the boss arrives.
- **Nod to the game.** Use its own vocabulary and imagery, e.g. `SCORE<1>` in Space Invaders, a laser base for each life, a bee for DoDonPachi's bees.
- **Keep motion short and meaningful.** Changes enter with the platform's `rise-in` animation, timed by `--motion` (150 ms), and a loop only while it signals a state, like the hovering UFO. Nothing that pulls the eye from the game when nothing is happening.
- **Never at the expense of latency.** A new value appears at once, and any animation starts from it ([gadgets.md](gadgets.md#keeping-gadgets-fast)). Use CSS animation, not JavaScript timers.

## 4. Steady

The overlay sits beside a game that needs the player's full attention. Anything that moves catches the eye, so the overlay mustn't move unless it means something. Values change in place; the layout never does.

- **Size a gadget for everything it can show, not what it shows now.** Its size is decided by the game, not by the current frame.
- **Lines don't come and go.** A note, a change indicator or a status line that is only sometimes shown keeps its space when it's empty. Leave the element in place with no text, or hide it with `visibility: hidden`, never by not rendering it.
- **Values keep a fixed width.** Use tabular figures (the platform's `.value` does) and pad numbers to their maximum digits, as `Score` does. Text that swaps between states, such as `PAUSED` and `100%`, gets a `min-width` that fits the longest.
- **Draw every pip, and fill some.** Bees draw all 13 and fill the collected ones, and power draws all 4 levels. A row that counts down, such as kills until the next speed-up, may shrink along its line, but the row keeps its height when it's empty (a `min-height` of one pip).
- **Lists reserve their rows.** DoDonPachi's splits table has a row for every stage a credit can reach, 1-1 to 2-7, from the start, with `-` in the ones not reached. Rank lists all its parts, dimming those that are 0.
- **Bars are always drawn**, empty when there's nothing to show.
- **Animate with `transform` and `opacity` only.** They don't move anything else on the page.
- **The exception:** a gadget whose variable isn't in the game's map renders nothing at all. That's decided once, when the game loads, not from frame to frame.

## Checklist for a new gadget

- [ ] Built from the platform's parts: `.gadget`, `.label`, `.value`, and `.unit`, `.bar`, `.pips` and `.note` where they fit
- [ ] Only tokens in its CSS, no raw colours or sizes, and colours used for their shared meanings
- [ ] Sizes in `rem` (only hairlines in `px`)
- [ ] Shows `-` for a null value, and renders nothing if the variable isn't in the map
- [ ] Reserves its full size up front: nothing appears, disappears or changes width while the game runs
- [ ] Uses an icon wherever one reads faster than words, with an accessible name
- [ ] No game assets: emoji or our own SVG only
- [ ] Has at least one touch that reacts to the game, if there's a natural one
- [ ] Animations are short, CSS-only, and never delay a value
