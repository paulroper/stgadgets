# Latency measurements

Results for the [latency requirements](requirements.md#latency-highest-priority), newest first.

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
