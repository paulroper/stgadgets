# 0002. RetroArch is the first emulator

- **Status:** Accepted, being re-checked. The milestone 0 spike compares RetroArch with MAME's Lua plugin system; ADR 0009 will confirm or replace this record.
- **Date:** 2026-10-15

## Context

Shmups are played on many emulators (MAME, FBNeo standalone, RetroArch, BizHawk and others). Supporting all of them from the start would hide whether the core idea works.

## Decision

Support RetroArch first, through a provider behind a shared interface (see [0004](0004-providers-use-official-emulator-apis.md)).

## Consequences

- One provider reaches many systems through RetroArch's cores, including arcade cores, on both macOS and Windows.
- RetroArch has a built-in network command interface that can read core memory.
- What we can read depends on each core exposing a memory map, which varies between cores and has to be checked core by core.
- Users must run RetroArch windowed or borderless (see [0003](0003-transparent-overlay-window.md)).
- Other emulators can be added later as new providers, without changing maps or gadgets.
