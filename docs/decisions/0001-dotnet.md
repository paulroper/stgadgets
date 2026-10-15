# 0001. Build with .NET 10

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

stgadgets is a desktop app that polls emulator memory about 60 times a second, decodes it, serves it to a UI, and manages a native overlay window. It needs to run on macOS and Windows.

The project is built with heavy AI assistance. The maintainer wants to be able to read, review and own the code properly, not just accept generated code they can't judge.

## Decision

Use C# on .NET 10, the current long-term support release, for everything except the gadget UI (see [0006](0006-react-gadgets-in-a-webview.md)).

## Consequences

- The maintainer already knows the language and ecosystem, so they can review and steer the code. This is the main reason.
- It runs on macOS, Windows and Linux.
- Good performance for the read → decode → send loop, with `Span<T>` and `BinaryPrimitives` for decoding without extra copies.
- Kestrel provides the local HTTP and WebSocket server with nothing extra to install.
- Native calls (P/Invoke) and the .NET for macOS workload give access to Cocoa and Win32 for the overlay window.
- Contributors need the .NET 10 SDK, and on macOS also Xcode, for the overlay host.
