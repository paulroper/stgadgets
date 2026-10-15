# 0003. Draw in a transparent window over the emulator

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

Gadgets have to appear with the game. The options were:

1. **A transparent window** that sits on top of the emulator window.
2. **Hooking the graphics:** inject into the emulator process and draw in its render path.
3. **The emulator's own overlay system,** such as RetroArch's overlays and shaders.

## Decision

Use a borderless, transparent, click-through window that stays on top and follows the emulator window.

## Consequences

- Nothing is injected into the emulator, so it works the same with any emulator that has a window. This fits the good-citizen approach in [0004](0004-providers-use-official-emulator-apis.md).
- Exclusive fullscreen isn't supported. Users run windowed or borderless.
- The overlay has to track the emulator window's position and size, and work out where the game picture sits inside it (aspect ratio, scaling, letterboxing, rotation).
- Click-through, no-focus and topmost behaviour need native code for each OS (see [0007](0007-native-overlay-host.md)).
- The overlay and the game picture may be a frame apart. That's fine for readouts and meters, but it could matter for hitbox overlays later.
