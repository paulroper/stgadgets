# 0005. Develop for macOS first

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

stgadgets should eventually run on macOS and Windows, and most shmup players are on Windows. However, development happens on a Mac, and the parts that differ between operating systems (the overlay window and window tracking) are only a small part of the code.

## Decision

Build and test on macOS first. Do Windows support in dedicated sessions on a Windows PC.

## Consequences

- Fast iteration: everything can be run and debugged on the development machine.
- Everything except the overlay host (core, providers, server, SDK, gadgets) must stay platform-neutral: no macOS-only APIs outside `Stgadgets.Host.Mac`.
- macOS-specific problems get solved first: building the overlay with the .NET for macOS workload, transparency, Spaces and fullscreen, and permissions for window tracking.
- Windows problems (WebView2, layered windows, DPI) are found later, in a separate session.
- Space Invaders and other test content must run in RetroArch on macOS.
