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
- **The recording analysis tools move into the CLI:** `recording markers`, `search`, `history`, `values`, `mark`
  and `trim` replace the skill's `recording.py`. `history` reads addresses with the map's type names and byte
  order, and can follow map variables and derived values through the pack.
- **Capture stays in the skill for now.** `record.py` talks to RetroArch directly while someone plays. Moving it
  would mean sharing the RetroArch provider's client, which lives in the server module.
- The `game-recording` skill keeps the workflow (what to record, how to search, when to mark) and calls the CLI.

## Consequences

- Recording analysis uses the same decoding and `derived.js` engine as the server and the tests, so what the CLI
  shows is what the overlay would show.
- The tools are tested with the rest of the .NET code (`dotnet test src/cli`) and work outside Claude, for pack
  authors as well as Claude sessions.
- Using them needs the .NET SDK rather than only Python, which every contributor to this repository has anyway.
- `src/lib/` is an exception to "modules never reference each other". It's limited to libraries with no process of
  their own; anything that runs talks to the server over HTTP.
- Recordings stay local and git-ignored; the rest of ADR 0011 stands.
