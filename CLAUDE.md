# stgadgets

Emulator overlay that shows M2 ShotTriggers-style gadgets for shmups. A .NET server reads game memory through emulator APIs and decodes it with per-game YAML memory maps. React gadgets display the values, in a browser or in a transparent native overlay. Start with `README.md`. Reference material lives in `docs/`: architecture, requirements, maps, providers, gadgets, latency results, and decision records in `docs/decisions/`.

## Key rules

- **Latency comes first** (`docs/requirements.md`): never add lag to the emulator, and keep the overlay within 1–2 frames of the game.
- No game-specific logic in the platform. Everything about a game lives in its pack under `packs/<parent romset>/` (`docs/packs.md`).
- Read emulators only through their official APIs. Never write to game memory.
- Memory maps use the original hardware's addresses. Emulator-specific address translation belongs in providers.
- Modules (`src/server`, `src/host/*`, `src/ui`) talk over HTTP/WebSocket on 127.0.0.1, never through project references.

## Build

- Server, core and tests (any OS): `dotnet test src/server`
- macOS overlay (needs Xcode + `macos` workload): `dotnet build src/host/macos`
- UI: `cd src/ui && npm run build`; tests (in `tests/ui`): `cd src/ui && npm test`

## Code conventions

@docs/conventions.md
