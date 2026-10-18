# 0013. Gadgets draw their own icons and don't ship game assets

- **Status:** Accepted
- **Date:** 2026-10-18

## Context

Gadgets read faster as pictures than as labels: a row of ships for lives, a bee for DoDonPachi's bees. The easiest pictures would be the game's own sprites, but those are copyrighted, and stgadgets and its packs are meant to be shared freely. Ripped sprites would also clash with each other and with the overlay's palette.

## Decision

- **Gadgets use emoji or SVG we draw ourselves** for icons. Our own drawing of a game's ship is fine, as long as it isn't a pixel-for-pixel copy of the original. An asset extracted from the game never is.
- **No game assets in the repository or in packs:** no sprites, screenshots, fonts, logos or sound from a ROM or its artwork.
- The design language that follows from this is in [gadget design](../gadget-design.md).

## Consequences

- Packs can be published without asset rights to worry about.
- Icons follow the overlay's palette and style, so gadgets from different packs look like one set.
- Some icons are less faithful than the real thing. Emoji also look different on each OS.
- **Possible extension:** a pack could describe sprites, and stgadgets could take them from the user's own ROM or emulator at runtime, so they're never distributed. That would need its own record, covering how providers expose graphics and how the webview's Content Security Policy allows them.
