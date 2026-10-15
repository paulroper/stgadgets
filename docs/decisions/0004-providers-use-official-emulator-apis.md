# 0004. Memory maps use the original hardware's addresses; providers use official emulator APIs

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

Gadgets need the game's internal state. We want a description of a game (its **memory map**) to work wherever the game runs, not to be tied to one emulator.

There are two broad ways to read an emulator's memory:

1. **Official interfaces:** RetroArch network commands (`READ_CORE_MEMORY`), MAME's Lua plugins, BizHawk's scripting API.
2. **Reading the process directly:** find where the emulator keeps the game's RAM inside its process and read it with OS APIs (`ReadProcessMemory`, `mach_vm_read`).

## Decision

- Write memory maps in **the original hardware's addresses** (the addresses a disassembly of the game would use), matched to specific ROM sets.
- Put emulator-specific details, including address translation, in **providers** behind one interface.
- Providers use **official, supported emulator interfaces**. Reading another process's memory is out of scope.

## Consequences

- One map works with any provider and any core that can reach the game's memory.
- Providers keep working across emulator updates, need no special OS permissions, and work the same on macOS and Windows. Reading another process directly is especially awkward on macOS.
- We depend on what each emulator exposes. RetroArch cores without a memory map can't be supported until they get one.
- RetroArch's interface is polled over UDP, so reads must be batched per tick, latency needs measuring, and polling isn't synced to frames (see [architecture](../architecture.md#polling-isnt-frame-synced)).
- Providers are read-only. stgadgets never writes to game memory.
