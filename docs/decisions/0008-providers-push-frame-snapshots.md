# 0008. Providers push frame snapshots

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

Overlay lag is a top-priority requirement ([L2](../requirements.md#l2-the-overlay-must-keep-up-with-the-game)). Some emulators can push data once per emulated frame (MAME's `register_frame_done`). Others can only be polled (RetroArch network commands). If the core polled providers on its own timer, frame-synced providers would suffer from polling delay they don't need.

## Decision

Providers **push** a stream of memory snapshots (`IAsyncEnumerable<Snapshot>`). Frame-synced providers emit one snapshot per emulated frame, labelled with the frame number. Polling providers run their own polling loop and emit what they read. The core handles each snapshot the moment it arrives, with no timers or batching.

## Consequences

- Frame-synced providers get the lowest possible delay: emulated frame → overlay with no waiting.
- Each tick has a frame number where the emulator provides one, so trackers can detect missed or repeated frames.
- Polling strategy (how fast to poll, pipelining requests) is each provider's own business.
- The replay provider fits naturally: it's just another stream.
- The core must keep up with the snapshot rate. Decoding a few hundred bytes at 60 Hz is trivial, but large maps (DoDonPachi) should be profiled.
