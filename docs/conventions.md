# Code conventions

## Organise code by what it does, not what it is

Group code by the job it does: the feature, responsibility or module. Don't group it by the kind of thing it is.

| ✅ By what it does | ❌ By what it is |
|---|---|
| `Core/Maps/`: loading, matching and decoding memory maps | `Core/Models/`, `Core/Services/` |
| `Core/Memory/`: address ranges, read plans, snapshots | `Core/Records/`, `Core/Structs/` |
| `Server/GameState/`: the game loop and the broadcaster that publishes it | `Server/Services/`, `Server/BackgroundServices/` |
| `ui/src/gadgets/`, `ui/src/layouts/`, `ui/src/sdk/` | `ui/src/components/`, `ui/src/hooks/`, `ui/src/utils/` |

Why:

- **Things that change together live together.** A change to how maps work touches one folder, not a model folder, a service folder and an interface folder.
- **The layout explains the system.** Reading the folder names tells you what stgadgets does: maps, memory, providers, game state. Folders named after kinds of thing tell you nothing.
- **Boundaries stay visible.** If a folder starts reaching into another one's internals, that's easy to spot, and it's a sign the code belongs elsewhere.

### Rules of thumb

- **No folders named after kinds of thing:** `Models`, `Services`, `Interfaces`, `Helpers`, `Utils`, `Extensions`, `Dtos`, `Constants`, `components`, `hooks`. If you're tempted to create one, ask what the code inside is *for*, and name the folder after that.
- **Keep a type next to what it serves.** For example, `EmulatorTarget` lives in the same file as `EmulatorLocator`, which returns it, and `ContentInfo` lives with `IEmulatorProvider`. An interface belongs with the feature that defines it, not in an `Interfaces/` folder.
- **Folder = namespace.** `Stgadgets.Core/Memory/` is `Stgadgets.Core.Memory`. Nothing lives in a project's root namespace unless it's genuinely about the project as a whole, such as `Program.cs`.
- **Add a folder when a responsibility emerges, not before.** One or two files can sit at the project root until a clear group forms.
- **Small, single-purpose files** are fine. A file can hold a few closely related types, such as a record and the class that produces it.

## The same idea at every level

The principle applies at every scale, from the repository down to the file:

| Level | Grouped by | Example |
|---|---|---|
| **Repository** | Module: what part of the system it is | `src/server/`, `src/host/`, `src/ui/`, `maps/`, `docs/` |
| **Module** | Area of responsibility | `server/providers/` (one project per emulator), `host/lib/` (shared by every host), `host/macos/` |
| **Project** | Feature | `Stgadgets.Core/{Maps,Memory,Providers,Recordings}`, `Stgadgets.Server/GameState` |
| **Tests** | Mirror of `src/` | `tests/server/Stgadgets.Core.Tests` tests `src/server/Stgadgets.Core` |

A few more rules for modules:

- **Each module builds on its own.** It has its own solution or package, so you only need its toolchain. The server builds on any OS without Xcode.
- **Modules talk over HTTP and WebSocket on `127.0.0.1`, never through project references.** The hosts and the UI depend on the server's API, not on its code. Shared code within a module goes in that module's `lib/`, as in `host/lib/`.
- **Tests mirror `src/`.** Tests for `src/<module>/X` live in `tests/<module>/X.Tests`. Shared test data, such as recordings, lives in `tests/fixtures/`.

See [Architecture → Repository layout](architecture.md#repository-layout) for the current tree.
