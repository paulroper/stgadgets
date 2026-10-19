# 0014. Gadgets share one look: plain CSS tokens, a global layout and a scale that follows the window

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Each pack styled its gadgets and layout on its own: raw colours in every stylesheet, layouts overriding the platform's panels, and pixel sizes tuned for one window. Gadgets from different packs drifted apart, panels on the same side had different widths, and the layout didn't fit small windows or use the room in large ones. Gadgets also added and removed lines as values came and went, so the overlay flickered beside a game that needs the player's full attention.

Pack UIs will one day be built separately and loaded at runtime ([ADR 0010](0010-game-packs.md)), so whatever keeps them consistent has to work without a shared build step.

## Decision

- **Plain CSS with custom properties, no framework.** Colours, sizes, spacing, radii and timing are tokens in the platform's `styles.css`, and colours have shared meanings (`--live`, `--good`, `--bad`). Gadgets use only tokens. We considered Tailwind, but it generates CSS by scanning source at build time, which suits neither separately built packs nor layouts restyling gadgets they don't own. Variables cross both boundaries.
- **Gadgets are built from shared parts:** `.gadget`, `.label`, `.value`, `.unit`, `.bar`, `.pips` and `.note`.
- **The platform owns the layout.** A grid of `left | game | right`, with the game's column left clear and each side a column of panels at one shared width. Packs use it and override tokens only when a game needs it. Grid sets the page's two-dimensional structure; flexbox stacks panels within a side.
- **The platform's styles are in `@layer platform`.** Pack CSS is unlayered, so it always wins, whatever order files load in.
- **Everything is sized in `rem`, and the root font size follows the window's height:** `clamp(12px, 1.75vh, 24px)`. A vertical game's picture is sized by the window's height, so the layout scales with the game. `vw` would grow panels in wide windows until they ran off the bottom. Only hairlines use `px`.
- **Gadgets reserve their space.** A gadget is sized for everything it can show: notes keep their line when empty, pip rows and lists reserve their full length, bars are always drawn, and animations use only `transform` and `opacity`.

The rules, with examples and a checklist, are in [gadget design](../gadget-design.md). The no-game-assets rule is [ADR 0013](0013-no-game-assets-in-gadgets.md).

## Consequences

- Gadgets from any pack look like one set, and a third-party pack gets the look by using the tokens and parts.
- Layouts need little or no CSS of their own. Both packs' layout stylesheets shrank to almost nothing.
- Reserved space makes gadgets taller. DoDonPachi's second-loop panel moved to the right side to fit.
- The layout scales between about 600px and 1400px of window height. Below the 12px floor it can overflow (DoDonPachi's left side, by about 45px at 600px tall). `npm run screenshot` in `src/ui` checks any window size.
- Scaling follows the window, not the game picture. Once `useGameRect` exists, the root font size could follow the picture's height instead, which changes only that one rule.
