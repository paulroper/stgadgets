# 0011. Recordings are local; recording tools live in a skill

- **Status:** Accepted; tools amended by [0015](0015-pack-tools-in-a-dotnet-cli.md): they're a .NET CLI, which the skill drives
- **Date:** 2026-10-16
- **Amends:** [0010](0010-game-packs.md) (recordings and expected values committed in packs)

## Context

[0010](0010-game-packs.md) put each game's recordings, and their `expected.yaml`, inside its pack, committed to the repository and checked by `dotnet test src/server`.

Recordings are large: about 3.6 MB per minute for each KB of RAM recorded. A 68000-era game with 64 KB of work RAM would be hundreds of MB per minute. Their real value is during **address discovery and verification**, when onboarding a game or chasing a bug, not as a permanent test suite. Committing them would bloat the repository for little ongoing benefit.

## Decision

- `packs/*/recordings/` is **git-ignored**. Recordings, and the `expected.yaml` beside them, are local working material.
- The tools for making and using recordings ship as a **project skill**, `.claude/skills/game-recording/`:
  - instructions for recording while the user plays, finding addresses, and verifying a map;
  - `record.py`, which captures a recording from RetroArch with live markers and change logging;
  - `recording.py`, which provides `search` (cheat-finder-style address filtering), `history`, `mark` and `trim`.
- The **test harness stays in the platform.** `PackRecordingTests` checks whatever recordings exist locally, and passes when there are none, as on a fresh clone or in CI.

## Consequences

- The repository stays small, and anyone onboarding a game records their own sessions.
- **Committed packs aren't continuously tested** against real play. A map can regress without CI noticing. If that becomes a problem, options include tiny trimmed recordings committed with Git LFS, or storing expected values with a reference to a recording kept elsewhere.
- The recording tools are Python scripts (standard library only) inside the skill, rather than a .NET `stgadgets record` command. They're developer tooling used through Claude, not part of the platform. A .NET CLI can still come later if pack authors need one outside Claude.
- The existing `packs/invaders/recordings/play-01` stays on the original machine only.
