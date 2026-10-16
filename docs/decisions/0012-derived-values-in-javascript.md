# 0012. Derived values are JavaScript, run by the server

- **Status:** Accepted
- **Date:** 2026-10-16

## Context

Some gadgets need values that aren't in memory but follow from it. Space Invaders' march speeds up at alien counts set by a table in ROM, so "kills until the march speeds up" needs the count from RAM plus the table from the disassembly.

The maths could live in the pack's React layout, but we want gadgets and layouts to **represent state with as little logic as possible**. Logic in the UI can't be tested against recordings, and other consumers (tests, OBS text sources, tools) can't use it.

The memory map can't hold it either. Lookup tables, thresholds, special cases and state carried between frames quickly outgrow an expression language, and we'd end up maintaining a weak interpreter of our own.

## Decision

- **`map.yaml` stays data.** Raw variables remain declarative YAML. The core builds read plans from it before anything runs, and the recording tools, `expected.yaml` and schema tooling read it without executing code.
- **A pack can add `derived.js`.** It's an ES module whose default export maps names to pure functions `(v, prev) => value`, or to `{ description, value }` objects that wrap one. `v` is this tick's values, including derived values declared above. `prev` is the previous tick's values, or `null` on the first tick.
- **The server computes derived values** each tick, straight after decoding. They're published alongside the map's variables, so gadgets just `useVar` them, and recordings check them in `expected.yaml`.
- **The engine is [Jint](https://github.com/sebastienros/jint)**, a JavaScript interpreter written in pure .NET. It has no native dependencies and it can be sandboxed: strict mode, no .NET access, no I/O, and limits on time (one frame per tick), memory and recursion.
- **Jint is confined to `Stgadgets.Core/Derived/`**, so it can be swapped for another engine without touching the rest of the platform.

## Consequences

- **Game logic now runs in the server, but it all comes from packs.** The platform still contains none ([ADR 0010](0010-game-packs.md)).
- **Pack authors write two languages:** YAML for the map and JavaScript for derived values. Layouts are TypeScript, so JavaScript is close to what they already write.
- **JavaScript numbers are doubles.** That's exact for every value a map decodes (at most 32 bits). The traps are elsewhere:
  - Bitwise operators work on signed 32-bit values, so decode bits in the map with `mask`/`shift` instead.
  - `/` returns fractions. Whole-number results become integers. Anything else stays a fraction.
- **Errors don't stop the loop.** A function that throws, returns `undefined`, or returns something other than a finite number, boolean, string or `null` gives `null` for that tick. The server logs it once, and the recording tests report it.
- **Cost is small.** On the Space Invaders recording, a tick of derived values takes about 16 µs. The evaluator is warmed up when the game is detected, because the engine's first calls take milliseconds.
- **Engines considered but not chosen:**
  - Python: IronPython is stuck on 3.4, and pythonnet needs CPython and the GIL inside the latency-critical process.
  - Lua: it's familiar from emulator scripting, but the pure .NET implementation (MoonSharp) only has doubles, and NLua needs a native library.
  - C# scripts: these can't be sandboxed well, and they break "adding a game needs no C#".
