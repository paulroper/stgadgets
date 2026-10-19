# 0015. Pack development tools are a .NET CLI on a shared Core library

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

[ADR 0011](0011-recordings-are-local.md) put the recording tools in a Claude skill as Python scripts, on the grounds
that they were developer tooling used through Claude, and left a .NET CLI for later.

Pack development has since grown tools that need the platform's own code. Starting game states and scenarios from
real play means replaying a recording through the pack's map and `derived.js` exactly as the server does, and a
second implementation of the decoder and the script engine in Python would drift from the server's. The planned
`stgadgets pack check` and `pack test` commands need the same code.

The server's `Stgadgets.Core` held that code, but modules may not reference each other's projects
([conventions](../conventions.md)), so a CLI couldn't use it from inside `src/server/`.

## Decision

- **A `stgadgets` CLI** in its own module, `src/cli/`, with its own solution. It's a .NET tool built with
  System.CommandLine ([cli.md](../cli.md)).
- **`Stgadgets.Core` moves to `src/lib/`,** a home for libraries shared between modules. It's the one place modules
  may reference; each module that uses a library includes it in its solution, so modules still build on their own.
- **The emulator providers move to `src/lib/providers/`** for the same reason: the server streams from them, and
  the CLI records from them. `Stgadgets.Providers` lists them all by ID, and neither the server nor the CLI names
  one in code: the server's configuration picks one (`Stgadgets:Provider`), and so does the CLI's `--provider`.
- **The recording tools move into the CLI,** replacing the skill's Python scripts. `recording record` captures
  through the provider interface, taking the core profile from the pack's provider settings as the server does, so
  it isn't tied to RetroArch's protocol. `markers`, `search`, `history`, `values`, `mark` and `trim` work on the
  result. `history` and `--watch` read addresses with the map's type names and byte order, and can follow map
  variables (and, for `history`, derived values) through the pack.
- The `game-recording` skill keeps the workflow (what to record, how to search, when to mark) and calls the CLI.

## Consequences

- Recording and analysis use the same provider, decoding and `derived.js` engine as the server and the tests, so a
  recording is what the server would have seen, and what the CLI shows is what the overlay would show. Neither
  is tied to one emulator: they pass the chosen provider its own endpoint and settings without interpreting them,
  so adding an emulator (MAME) means writing its provider and adding it to `EmulatorProviders`.
- The tools are tested with the rest of the .NET code (`dotnet test src/cli`) and work outside Claude, for pack
  authors as well as Claude sessions.
- Using them needs the .NET SDK instead of Python, which every contributor to this repository has anyway.
- Recordings made by the CLI also carry `capturedAt` in the manifest, which `RecordingManifest` now models.
- `src/lib/` is an exception to "modules never reference each other". It's limited to libraries with no process of
  their own; anything that runs talks to the server over HTTP. Two processes can now talk to RetroArch at once (the
  server and a recorder); both only read, so they can share it, but each takes its own share of RetroArch's
  one-command-per-frame budget.
- Recordings stay local and git-ignored; the rest of ADR 0011 stands.
