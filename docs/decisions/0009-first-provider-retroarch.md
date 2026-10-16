# 0009. RetroArch (via READ_CORE_RAM) is the first provider

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

The milestone 0 spike had to show we could read Space Invaders' memory, using the original hardware's addresses, on macOS. It also had to decide between RetroArch network commands and a MAME Lua plugin ([roadmap](../roadmap.md#0-pick-the-first-provider-spike-about-a-day)).

Results with RetroArch 1.22.2 and the FBNeo core (details in [providers.md](../providers.md#retroarch-provider) and [latency.md](../latency.md)):

- `READ_CORE_MEMORY` fails: FBNeo publishes no libretro memory map.
- `READ_CORE_RAM` works. Offset 0 is CPU address `0x2000`, so a one-line core profile translates addresses.
- Round-trips take about one frame, so polling back-to-back yields about one sample per frame, missing about 0.3% of frames.
- The whole path (provider → decode → WebSocket → React) worked the same day.

MAME wasn't tried. It isn't installed, and RetroArch was good enough to continue.

## Decision

RetroArch, using `READ_CORE_RAM` and a core profile per (system, ROM set), is the first provider. This confirms [0002](0002-retroarch-first.md).

## Consequences

- We can build on RetroArch without changing the user's setup beyond enabling network commands and turning off hardcore mode.
- Each core and game pairing needs a core profile (the RAM base address) until we find a general rule for each core.
- Polling isn't frame-synced, so single-frame events can occasionally be missed (about 0.3% of frames).
- **MAME Lua stays the planned second provider**, for frame-synced data and the original hardware's addressing without profiles. Revisit if missed frames or profiles become a problem (e.g. for hitbox overlays or DoDonPachi).
