# 0016. macOS, Windows and Linux are equal targets

- **Status:** Accepted
- **Date:** 2026-10-22
- **Amends:** [0005](0005-macos-first.md) (macOS and Windows as the only targets)

## Context

[ADR 0005](0005-macos-first.md) chose to develop on macOS first, with Windows to follow, and didn't plan for Linux.
Shmup players also play on Linux, including on handhelds such as the Steam Deck, and the parts of stgadgets that
differ between operating systems are still only the overlay host and window tracking.

## Decision

stgadgets supports macOS, Windows and Linux equally. Each gets an overlay host, an installable package and its own
latency checks, and a release isn't finished until it works on all three. Development still happens on macOS first,
as ADR 0005 says.

## Consequences

- A Linux overlay host joins the Windows one in the roadmap's [single distributable](../roadmap.md#4-single-distributable)
  milestone.
- Linux brings its own window problems: X11 and Wayland behave differently, and on Wayland an app can't find or follow
  another app's window. How to handle that needs deciding before the host is built
  ([ADR 0007](0007-native-overlay-host.md#linux-implementation-later)).
- The L1 latency check runs on each OS ([requirements](../requirements.md#l1-no-added-lag-in-the-game)).
- Code outside the hosts stays platform-neutral, as it already must.
