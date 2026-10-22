# Requirements

## Latency (highest priority)

STG players are extremely sensitive to lag. Latency is a requirement we test, not something we tune later.

There are two different kinds of lag, and they're handled separately:

### L1. No added lag in the game

stgadgets must add **zero** input or display latency to the emulator.

- No injection, no hooking the graphics, nothing running inside the emulator's process (see [ADR 0003](decisions/0003-transparent-overlay-window.md) and [ADR 0004](decisions/0004-providers-use-official-emulator-apis.md)).
- Very low resource use, so we never compete with the emulator. Target: under 5% of one CPU core, no continuous GPU work while gadgets are idle.
- **The overlay window must not knock the emulator off its fastest presentation path.** If our window sits on top of the game, the OS may have to composite the two together instead of showing the game's frames directly. That can add a frame. Watch for:
  - **macOS:** fullscreen apps can be shown directly without compositing, and a window over them may stop that.
  - **Windows:** "independent flip" and multiplane overlays for borderless windows. A window on top may force DWM composition.
  - **Linux:** compositors show fullscreen windows directly (unredirection on X11, direct scanout on Wayland), and a window on top may stop that.
- **Checked by measurement:** emulator latency with and without the overlay, on each OS and display mode we support. If an overlay mode adds latency, the docs and UI must say so.

### L2. The overlay must keep up with the game

A gadget shows memory from a game frame. **How far behind** the game picture it is, measured in display frames at 60 Hz:

| Provider | Target (95th percentile) | Hard limit |
|---|---|---|
| Frame-synced (pushes once per frame, e.g. MAME Lua) | ≤ 1 frame | 2 frames |
| Polling (e.g. RetroArch network commands) | ≤ 2 frames | 3 frames |

Notes:

- Games often show a frame or more **after** updating RAM, so the overlay can be *ahead* of the picture. The requirement applies to the gap in either direction.
- No smoothing, interpolation or debouncing that delays values. Animations in gadgets must start from the new value right away.
- The core never drops or merges ticks (see [architecture](architecture.md#the-loop)).

### L3. Measured continuously

- **Timestamps through the pipeline:** each tick carries timestamps for provider receive, decode, WebSocket send, JS receive and paint. A debug gadget shows them live.
- **End-to-end test:** record the screen at 60 fps or more and count the frames between the game's own score changing on screen and the score gadget changing. Space Invaders, with its on-screen score, is the reference test. `stgadgets latency analyse` counts the frames ([cli.md](cli.md#latency)), and the `latency-test` skill runs the test.
- Latency results are recorded for each provider and OS in [latency.md](latency.md).

### Where the overlay lag comes from

| Source | Typical | Mitigation |
|---|---|---|
| Polling that isn't frame-synced | 0–1 frame | Frame-synced providers that push data; poll faster than the frame rate with polling providers |
| Emulator handling commands once per frame | 0–1 frame per round-trip | Send every read for a tick together without waiting for replies; one round-trip per tick at most |
| Transport (UDP/socket → .NET → WebSocket) | < 1 ms | `TCP_NODELAY`, no buffering or batching timers, send as soon as decoded |
| Webview paint | 0–1 frame | Keep render work small; high-rate gadgets update through refs or canvas instead of React re-renders |
| OS compositor | ~1 frame | Not avoidable with a separate window; measure it |

## Other requirements

- **Read-only.** stgadgets never writes to emulator memory.
- **Official emulator APIs only.** See [ADR 0004](decisions/0004-providers-use-official-emulator-apis.md).
- **Local only.** The server listens only on `127.0.0.1`. stgadgets needs no internet connection.
- **Gadgets work without a running emulator** through the replay provider, for development and tests.
