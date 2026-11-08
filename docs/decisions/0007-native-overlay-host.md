# 0007. The overlay host is a native window with the system webview

- **Status:** Proposed (the macOS and Windows spikes passed; to be confirmed by the Linux host). Windows accepted with a latency limitation, 2026-11-08 ([below](#windows-decision))
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

## macOS implementation

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
| L1: no added emulator lag | ✅ So far: RetroArch was composited with or without the overlay (2026-10-20, [latency.md](../latency.md)). Still to check: a setup where RetroArch gets direct presentation |
| CPU | ~8% of one core in total (server 5.4%, host 1.4%, WebKit 1.5%) in Debug builds. Over the < 5% target, to be optimised |

Options 2 and 3 (Photino, Avalonia) weren't tried: option 1 took about 150 lines and covered everything we needed. If the remaining checks pass, this record becomes **Accepted**. Game Mode's throttling must be understood first: if it's specific to WebKit, a native renderer may be needed for fullscreen play, and that affects [0006](0006-react-gadgets-in-a-webview.md).

## Windows implementation

| Need | Approach |
|---|---|
| Transparent window | A borderless WinForms window with `WS_EX_NOREDIRECTIONBITMAP`, so it draws nothing of its own. WebView2 draws into it through DirectComposition ("visual hosting": `CoreWebView2CompositionController` with a root visual on the window), with a transparent default background. The usual windowed WebView2 control can't be transparent per pixel over another app |
| Click-through | `WS_EX_LAYERED \| WS_EX_TRANSPARENT`, with `SetLayeredWindowAttributes` at full opacity |
| Never take focus | `WS_EX_NOACTIVATE`, shown with `SetWindowPos(SWP_NOACTIVATE)`; `WS_EX_TOOLWINDOW` keeps it out of Alt-Tab and the taskbar |
| Stay above the emulator | `WS_EX_TOPMOST` |
| Follow the emulator's window | Poll at 30 Hz: `EnumWindows` for the largest visible, unowned, uncloaked window of the emulator's process, and its client area (`GetClientRect` + `ClientToScreen`), which already leaves out the title bar. The app is per-monitor DPI aware, so positions are physical pixels |
| Keep rendering in the background | Chromium's background throttling off (`--disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows`) |
| App | A tray app (`NotifyIcon`): state, reload, overlay bounds (an outline drawn by the page), DevTools, quit |

Pitfalls found:

- **Exclusive fullscreen:** nothing shows over an emulator in exclusive fullscreen, as [0003](0003-transparent-overlay-window.md) expected. The overlay shows its window and reports that it's following the emulator, but Windows never displays it. RetroArch needs **Settings → Video → Fullscreen Mode → Windowed Fullscreen Mode** on (`video_windowed_fullscreen = "true"`), or windowed play. User docs must say so.
- **WebView2's package** always references its WPF control, which warns about a WindowsBase version conflict in a WinForms app; the project drops that reference after the package adds it.
- **Snapshots:** `CoreWebView2.CapturePreviewAsync` keeps the page's transparency. `HttpListener` on `http://127.0.0.1:<port>/` needs no URL reservation or admin rights.
- **Latency ([L1](../requirements.md#l1-no-added-lag-in-the-game)):** a window on top of a borderless emulator can stop Windows from showing the emulator's frames directly (independent flip), forcing DWM composition and adding a frame. Check this with PresentMon.

### Spike results (2026-11-08)

`src/host/windows`, Windows 11, WebView2 SDK 1.0.4314.35, RetroArch 1.22.2 (Vulkan) with FinalBurn Neo.

| Check | Result |
|---|---|
| Per-pixel transparency through DirectComposition | ✅ The snapshot's empty areas have alpha 0 and the panels their CSS alpha |
| Overlay over RetroArch in borderless fullscreen | ✅ Checked by hand |
| Overlay over RetroArch in exclusive fullscreen | ❌ Not possible, as expected (see above) |
| Click-through, never taking focus | ✅ Checked by hand: clicks over the panels reach RetroArch, which keeps keyboard focus |
| Smoothly following moves and resizes (windowed) | ✅ Checked by hand. Holding a drag pauses RetroArch, which stops answering network commands; the RetroArch provider waits that out while its process is running, rather than dropping the game |
| L1: no added emulator lag (PresentMon) | ❌ In borderless fullscreen, the overlay drops RetroArch from `Hardware: Independent Flip` to `Composed: Flip`, about 10 ms later to the screen ([latency.md](../latency.md)). Windowed play is unaffected. This PC's GPU has no multiplane overlays (`MPO MaxPlanes: 1`), so nothing on top of a fullscreen game can avoid it here; GPUs that have them are untested |
| L2: overlay read → paint | p50 4.9 ms, p95 7.9 ms at 59 Hz (the layout's own gadget, while hidden); the end-to-end test is still to do |

The video driver made no difference: RetroArch's `d3d11` driver showed the same switch to `Composed: Flip`, as expected, since both drivers present through DXGI.

### Windows decision

**Accepted (2026-11-08), with a latency limitation:** on a GPU without multiplane overlays, the overlay adds about 10 ms in borderless fullscreen, breaking [L1](../requirements.md#l1-no-added-lag-in-the-game) there. The user docs say so, and recommend windowed play or a browser on another screen for players who want no added lag.

Ways around it that were considered:

| Option | Why not now |
|---|---|
| A GPU with multiplane overlays, so DWM can give the game and the overlay separate planes | Hardware we can't require. Whether DWM would give WebView2's DirectComposition content a plane of its own is untested |
| Drawing the gadgets into the emulator's own frames, as the Steam overlay and RivaTuner do | Runs inside the emulator's process, which [L1](../requirements.md#l1-no-added-lag-in-the-game) and [0003](0003-transparent-overlay-window.md) rule out. A Vulkan layer is the one official way in, but it covers only Vulkan and needs a native renderer instead of the webview ([0006](0006-react-gadgets-in-a-webview.md)) |
| A window that isn't on top of the game (a browser on another screen) | Already supported; it's the recommendation for zero added lag |

## Linux implementation (later)

- **X11:** a window with an ARGB visual, an empty input shape (XShape) so clicks pass through, and
  `_NET_WM_STATE_ABOVE` to stay on top. Window tracking with `XGetWindowAttributes` and `XTranslateCoordinates`.
- **Wayland:** an app can't find another app's window or place its own at a position on screen, so the X11 approach
  doesn't carry over. Options to weigh: running the host under XWayland, the `wlr-layer-shell` protocol (wlroots
  compositors and KDE, not GNOME), or a position the user sets.
- WebKitGTK with a transparent background.
- **Latency ([L1](../requirements.md#l1-no-added-lag-in-the-game)):** compositors show a fullscreen window directly
  (unredirection on X11, direct scanout on Wayland), and a window on top may stop that.
