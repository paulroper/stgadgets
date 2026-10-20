# Latency measurements

Results for the [latency requirements](requirements.md#latency-highest-priority), newest first.

## 2026-10-20: end-to-end test (L2)

The first [end-to-end test](requirements.md#l3-measured-continuously): screen recordings with Cmd-Shift-5 of the game's own score and the score gadget, measured with `stgadgets latency analyse` ([cli.md](cli.md#latency)). Offsets are in 60 Hz game frames, positive when the gadget is behind.

**Setup:** macOS 26.6.2 on an M1 Max, RetroArch 1.22.2 (Vulkan driver, vsync off), FBNeo core, Space Invaders, RetroArch provider (polling). RetroArch is fullscreen on an external 2560×1440 display, at 144 Hz for the first run and 60 Hz for the others. The overlay panel sits beside the game.

| Run | Pairs | Offset p50 / p95 / min / max | By frame | Overlay paint (rAF) | Verdict |
|---|---|---|---|---|---|
| **Baseline: run-ahead off, VRR off, overlay freshly started** | **41 of 42** | **+1.0 / +2.0 / 0.0 / +3.0** | 0: 4, +1: 25, +2: 10, +3: 2 | 60 Hz, read → paint 7 / 16 ms | ✅ Meets L2 for polling providers (p95 ≤ 2 frames, max ≤ 3) |
| Run-ahead off, overlay painting at half rate | 43 of 44 | +3.0 / +5.0 / +1.0 / +5.9 | +1: 2, +2: 18, +3: 8, +4: 12, +5: 2, +6: 1 | 30 Hz, read → paint 19 / 34 ms | ❌ |
| Run-ahead off, half rate, short run | 15 of 15 | +2.0 / +4.0 / 0.0 / +4.0 | 0: 1, +1: 4, +2: 6, +3: 2, +4: 2 | 30 Hz, read → paint 20 / 32 ms | ❌ (too few changes to rely on) |
| Run-ahead on (3 frames), at 144 Hz | 53 of 54 | +5.0 / +5.9 / +4.1 / +6.7 | +4: 8, +5: 36, +6: 8, +7: 1 | 72 Hz (half rate), read → paint 8 / 15 ms | ❌ |

The capture recorded about 60 fps (one frame every 16.7 ms at p50, 33 ms at p95), so offsets are accurate to about a frame. The run at 144 Hz captured at about 66 fps, one frame every 15 ms. In the baseline, the overlay's frames came every 17 ms (p95 18–22 ms, at most 1% skipped), and its main-thread work per frame (rAF callbacks, style, layout and paint) was 1 ms p50, 2–5 ms p95 and 11 ms at most, at WebKit's 1 ms timer resolution: well inside a frame.

**Run-ahead puts the picture N frames ahead of the memory we read.** With run-ahead, RetroArch shows a frame from N frames in the future and then rolls the core back, and `READ_CORE_RAM` reads the core after the rollback. No reader of memory through the official API can see those frames. With 3 frames of run-ahead the gadget was 5 frames behind, against 1 without. Players use run-ahead in shmups, so the docs and UI must say that it adds N frames to the overlay (as [L1](requirements.md#l1-no-added-lag-in-the-game) already asks for any mode that adds latency). Preemptive frames presumably do the same, but that hasn't been tested.

**An overlay can get stuck painting at half the display's rate.** The overlay started at 19:49 painted at exactly half the refresh rate for its whole life: 72 Hz at 144 Hz, and 30 Hz at 60 Hz. At 30 Hz, read → paint more than doubles (19 ms p50, 34 ms p95) and the test fails. Restarting the overlay cleared it: the new one painted at the full rate, 144 Hz at 144 Hz and 60 Hz at 60 Hz, and kept it across display mode changes. Ruled out: Low Power Mode, battery power, RetroArch's VRR runloop (turned off with no effect), and our own render cost. The cause is unknown. The latency reports now include the rAF interval and per-frame work, so it shows in the server log as intervals of twice the display's period if it happens again. The 2026-10-15 measurement of 60 Hz on the built-in 120 Hz display may have been the same thing.

## 2026-10-15: overlay read → paint

Measured by the SDK's `LatencyTracker`, which reports to the server log every 5 s. "Arrive" is when the WebSocket message reaches the page; "paint" is the next `requestAnimationFrame`.

| RetroArch mode | read → paint p50 / p95 / max | read → arrive | Paint rate | Verdict |
|---|---|---|---|---|
| **Windowed** | **14 / 16 / 22 ms** | 0.3 ms | 60 Hz | ✅ Within 1 frame (meets L2) |
| **Fullscreen, Game Mode on** | 100 / 200 / 220 ms | 0.3–1 ms | **5 Hz** | ❌ 6–12 frames behind |
| **Fullscreen, Game Mode off** | 6 / 17 / 26 ms | 0.3 ms | 60 Hz | ✅ Within 1 frame |
| Overlay over another app (control) | 13 / 15 / – ms | 0.3 ms | 60 Hz | ✅ |

**Fullscreen throttling: confirmed as macOS Game Mode.** Game Mode switches on automatically when a game goes fullscreen. It gives the game priority on the CPU and GPU and lowers the priority of other apps. Under Game Mode, WebKit runs the overlay page's rendering updates at only 5 Hz, while timers still run at about 50 Hz and the page reports `visible`. With Game Mode turned off for RetroArch (menu bar game controller icon → Turn Off Game Mode), fullscreen matches windowed mode.

**Tried, with no effect** (all removed again):
- Holding an App Nap / latency-critical activity: `NSProcessInfo.BeginActivity(UserInitiated | LatencyCritical)`. Kept, since it's harmless.
- Turning off WebKit's `BackgroundWebContentRunningBoardThrottlingEnabled` and `DOMTimersThrottlingEnabled` through private API.
- A "heartbeat" pixel changing every frame, to stop the window server treating our app as visually idle.

**Still open:**
- Whether a native renderer (Core Animation or Metal, not WebKit) is throttled the same way under Game Mode. Deferred: we're keeping the web UI and asking users to turn off Game Mode ([ADR 0006](decisions/0006-react-gadgets-in-a-webview.md)).

## 2026-10-15: first end-to-end spike

**Setup:** macOS, RetroArch 1.22.2, FBNeo core, Space Invaders. RetroArch provider (`READ_CORE_RAM`, polling). Layout in a browser tab, not yet the overlay.

| Stage | p50 | p95 | Notes |
|---|---|---|---|
| RetroArch round-trip | 16.3 ms | 19.2 ms | About 1 frame. RetroArch handles commands once per frame |
| Sample interval | 16.7 ms | 19.6 ms | 0.3% of intervals over 25 ms (missed frame), 0.4% under 8 ms (same frame twice) |
| Server read → WebSocket client | 0.2–0.3 ms | 2.5–3.6 ms | Measured with a Node client |
| Server read → browser paint | – | – | Shown live by the `LatencyMeter` gadget; record values here |

**Estimated overlay lag (L2):** up to 1 frame on the emulator side, plus well under 1 ms through the pipeline, plus about 1 frame for the webview to paint. That's about 1–2 frames, within the polling-provider target of 2 frames or fewer.

**Overlay (same day):** the macOS overlay host shows the same layout in a transparent WKWebView over RetroArch, including in fullscreen. CPU use is about 8% of one core in total (server 5.4%, overlay app 1.4%, WebKit 1.5%) in Debug builds. That's over the [L1](requirements.md#l1-no-added-lag-in-the-game) target of under 5%, so the server's per-tick work needs optimising.

**Not yet measured:**
- The end-to-end screen-recording test (L3).
- Compositor cost and any lag the overlay adds to the emulator (L1). Both need the native overlay.
