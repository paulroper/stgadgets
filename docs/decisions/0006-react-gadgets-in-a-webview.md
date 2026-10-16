# 0006. Gadgets are React components running in a webview

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

Gadgets are the part of stgadgets most likely to be written by other people, and the part where how things look matters most. The maintainer knows React. The options were:

1. **A .NET UI framework** (Avalonia, MAUI): gadgets written in XAML/C#.
2. **Drawing directly** (SkiaSharp): gadgets written in C# drawing code.
3. **Web technologies:** gadgets written as React components and displayed in a webview.

## Decision

Gadgets and layouts are React components written in TypeScript, built ahead of time into static bundles, and displayed in the OS's built-in webview. The .NET app serves them from a local HTTP server and streams variables over WebSocket. An SDK (`@stgadgets/sdk`) provides hooks (`useVar`, …) and also exposes the variables as a `window.stgadgets` global.

## Consequences

- Writing gadgets uses the most widely known UI skills there are, so it's accessible to contributors. CSS, animation and hot reload come for free.
- **OBS support for free:** a layout is just a URL, so it works as a browser source.
- Gadgets can be developed in an ordinary browser against the replay provider, with no emulator or overlay.
- There's no Node.js at runtime: the system webview (WKWebView or WebView2) runs the JavaScript. Node is only needed to build the web bundles.
- The repository uses two languages (C# and TypeScript), with two toolchains to maintain.
- Sending data from .NET to the webview costs serialization and a WebSocket hop each tick. For a few dozen variables at 60 Hz this should be negligible, but needs measuring. Drawing many objects per frame (bullet or hitbox overlays) may need a canvas-based gadget instead of DOM elements.
- Third-party layouts run arbitrary JavaScript. They're sandboxed by serving only from `127.0.0.1` with a strict Content Security Policy.
- **macOS Game Mode throttles the web view** to 5 Hz rendering (about 100–200 ms behind) while a game is fullscreen ([latency.md](../latency.md)). On 2026-10-15 we chose to keep the web UI anyway, for its development and UI benefits, and to ask users to turn off Game Mode for their emulator, or play windowed. We haven't checked whether a native renderer would avoid the throttling. Revisit if requiring Game Mode to be off turns out to be a problem for players.
