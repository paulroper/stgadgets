# 0007. The overlay host is a native window with the system webview

- **Status:** Proposed (needs a spike on macOS)
- **Date:** 2026-10-15

## Context

The overlay must be a transparent, click-through, never-focused window that stays on top ([0003](0003-transparent-overlay-window.md)) and displays web content ([0006](0006-react-gadgets-in-a-webview.md)).

.NET makes the runtime and libraries cross-platform (files, sockets, HTTP, threads) but not window-manager behaviour. Some of what we need isn't covered by .NET's UI frameworks either:

| Need | Why frameworks don't cover it |
|---|---|
| Clicks pass through to the app underneath | Frameworks handle clicks *inside* your own window. Passing them to another app is an OS window-manager setting |
| Stay above another app, including over fullscreen | Window levels, macOS Spaces and Windows' always-on-top each work differently |
| Find another app's window position | Reading another process's windows is an OS API |
| Transparent webview | Each system webview has its own transparency switch |

The options were:

1. **Native window and system webview for each OS:** `NSWindow` and `WKWebView` through the .NET for macOS workload, and a Win32 layered window with WebView2 on Windows.
2. **A cross-platform webview wrapper** (such as Photino.NET): one API over WKWebView, WebView2 and WebKitGTK.
3. **A cross-platform UI framework** (Avalonia) with an embedded webview.

## Decision

Proposed: option 1. The overlay host is a thin native shell for each OS. Each one has its own project (`Stgadgets.Host.Mac`, later `Stgadgets.Host.Windows`) and no business logic. It creates the window, hosts the webview, and follows the emulator window.

The spike also tries options 2 and 3. If one covers enough to cut down the native code, this record will be replaced with one choosing it.

## Consequences

- Full control over the window details that matter: transparency, ignoring mouse events, window level, Spaces and fullscreen behaviour.
- Options 2 and 3 would still need native calls for click-through and for tracking another app's window, so they save less than it first appears.
- Two small host projects (a few hundred lines each) instead of one. That's acceptable, since the shared core does all the real work.
- The .NET for macOS workload needs Xcode (each workload release needs a specific Xcode version) and produces an `.app` bundle, which affects how we build and run during development.

## macOS implementation (to confirm in the spike)

| Need | Approach |
|---|---|
| Transparent window | `NSWindow` with `isOpaque = false` and a clear background colour; `WKWebView` with its background drawing turned off |
| Click-through | `ignoresMouseEvents = true` |
| Stay above the emulator | Window level of floating or higher |
| Show over fullscreen Spaces | `collectionBehavior` including `.canJoinAllSpaces` and `.fullScreenAuxiliary` |
| Follow the emulator's window | Poll `CGWindowListCopyWindowInfo` for the emulator process's window bounds (check what permissions this needs) |

Pitfalls to check:

- **App Transport Security:** WKWebView refuses to load `http://127.0.0.1` unless Info.plist sets `NSAllowsLocalNetworking`. This happens with any framework that uses WKWebView.
- **Two coordinate systems:** CGWindowList reports window positions with the origin at the top-left, while Cocoa windows put it at the bottom-left. Retina scaling (points vs pixels) and multiple monitors add further conversions.
- **Main thread only:** AppKit and WKWebView calls must happen on the main thread. The provider and the server must never block it. This is true of every UI toolkit.
- **Latency ([L1](../requirements.md#l1-no-added-lag-in-the-game)):** check whether our window over a fullscreen emulator stops macOS from showing the emulator's frames directly. Also measure how long the webview takes to paint a tick.

## Spike results (2026-10-15)

`src/host/macos`: a menu bar app (`LSUIElement`) with the approach above, Xcode 27.1, macOS 26.6.

| Check | Result |
|---|---|
| Transparent `NSWindow` and `WKWebView` (`drawsBackground = false`) | ✅ The game shows through around the panel |
| WKWebView loads `http://127.0.0.1:5199` with `NSAllowsLocalNetworking` | ✅ |
| Overlay over RetroArch in a fullscreen Space (`.fullScreenAuxiliary`) | ✅ |
| Window tracking with `CGWindowListCopyWindowInfo` (owner name and bounds), no permission prompt | ✅ Polled at 30 Hz. Hidden whenever RetroArch isn't the frontmost app |
| Click-through, never taking focus | To confirm by hand |
| Smoothly following moves and resizes | To confirm by hand |
| L2: overlay read → paint | ✅ Windowed: p50 14 ms, p95 16 ms. Fullscreen: ✅ with Game Mode off (p50 6 ms); ❌ with Game Mode on (5 Hz rendering, ~100–200 ms) ([latency.md](../latency.md)) |
| L1: no added emulator lag | ❓ Not yet measured |
| CPU | ~8% of one core in total (server 5.4%, host 1.4%, WebKit 1.5%) in Debug builds. Over the < 5% target, to be optimised |

Options 2 and 3 (Photino, Avalonia) weren't tried: option 1 took about 150 lines and covered everything we needed. If the remaining checks pass, this record becomes **Accepted**. Game Mode's throttling must be understood first: if it's specific to WebKit, a native renderer may be needed for fullscreen play, and that affects [0006](0006-react-gadgets-in-a-webview.md).

## Windows implementation (later, separate session)

- A layered window that's transparent and click-through: `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE`.
- WebView2 with a transparent default background colour.
- Window tracking with `GetWindowRect` or WinEvent hooks.
- **Latency ([L1](../requirements.md#l1-no-added-lag-in-the-game)):** a window on top of a borderless emulator can stop Windows from showing the emulator's frames directly (independent flip), forcing DWM composition and adding a frame. Check this with PresentMon.
