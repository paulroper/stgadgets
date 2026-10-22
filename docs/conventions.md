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
| **Repository** | Module: what part of the system it is | `src/server/`, `src/cli/`, `src/host/`, `src/ui/`, `src/lib/` (shared), `packs/`, `docs/` |
| **Module** | Area of responsibility | `lib/providers/` (one project per emulator), `host/lib/` (shared by every host), `host/macos/` |
| **Project** | Feature | `Stgadgets.Core/{Maps,Memory,Packs,Providers,Recordings}`, `Stgadgets.Server/GameState` |
| **Tests** | Mirror of `src/` | `tests/lib/Stgadgets.Core.Tests` tests `src/lib/Stgadgets.Core` |

A few more rules for modules:

- **Each module builds on its own.** It has its own solution or package, so you only need its toolchain. The server builds on any OS without Xcode.
- **Modules talk over HTTP and WebSocket on `127.0.0.1`, never through project references.** The hosts and the UI depend on the server's API, not on its code. Shared code within a module goes in that module's `lib/`, as in `host/lib/`.
- **Code shared between modules goes in `src/lib/`,** the one place modules may reference. It's for libraries with no running process of their own, such as `Stgadgets.Core` (packs, maps, recordings) and the emulator providers, which the server and the CLI both use. Each module that uses one includes it in its own solution, so the module still builds on its own.
- **Tests mirror `src/`.** Tests for `src/<module>/X` live in `tests/<module>/X.Tests`. Game recordings and their expected values live in each pack's `recordings/` folder, which is local and git-ignored (`docs/packs.md#testing`).

See [Architecture → Repository layout](architecture.md#repository-layout) for the current tree.

## Comments explain decisions, not code

Code should explain itself through its names, types and structure. Use a comment only to explain a decision or give the context behind it: why the code is this way, what it rules out, or where a value comes from.

| ✅ Explains a decision | ❌ Restates the code |
|---|---|
| `// Setting onmessage starts a MessagePort; addEventListener needs a separate start() call.` | `// Set the message handler` |
| `const PAUSED_AFTER_TICKS = 30; // longer than slowdown ever holds a frame` | `const PAUSED_AFTER_TICKS = 30; // 30 ticks` |
| `// Never let a provider error stop the app; log it and reconnect.` | `// Catch the exception` |

If code needs a comment to say *what* it does, improve the code first: rename it, extract a well-named function or constant, or simplify it. The same goes for doc comments: write one when it adds context that the name and signature don't give.

## Linting

### .NET

Linting is part of the build: `dotnet build` runs the Roslyn analyzers (`AnalysisLevel=latest-recommended`) and the code style in the root `.editorconfig`. Any warning fails the build (root `Directory.Build.props`), locally and in CI. To fix most style issues automatically, run `dotnet format` on the module's solution, for example `dotnet format src/server/Stgadgets.Server.slnx`.

Turn a rule off in `.editorconfig`, with a comment that says why, rather than suppressing it in code.

### TypeScript and JavaScript

[Oxlint](https://oxc.rs/docs/guide/usage/linter) and [Oxfmt](https://oxc.rs/docs/guide/usage/formatter) cover the UI (`src/ui`), its tests (`tests/ui`) and the packs: their UIs, tests and `derived.js`. Run them from `src/ui`:

- `npm run lint`: Oxlint, with the rules in the root `.oxlintrc.json`, including type-aware rules such as `no-floating-promises`. Any warning fails it.
- `npm run format`: formats the files in place. `npm run format:check` only checks them, as CI does.

Their configs (`.oxlintrc.json`, `.oxfmtrc.json`) live at the repository root because they cover packs and tests as well as `src/ui`. Turn a rule off there, with a comment that says why, when it doesn't fit this codebase. For a single false positive, disable it on that line with `// oxlint-disable-next-line <rule>`, after a comment that says why.

The root `tsconfig.json` only refers to `src/ui/tsconfig.json`. Packs and `tests/ui` have no tsconfig of their own, and without it, editors and the type-aware rules would check them with TypeScript's defaults.

## .NET project settings and packages

- **Shared settings** (target framework, nullable, implicit usings, linting) live in the root `Directory.Build.props`, so a project file holds only what's particular to it. `tests/Directory.Build.props` adds the test packages and `using Xunit` to every test project.
- **Package versions** live in the root `Directory.Packages.props` ([Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)). A project's `<PackageReference>` names a package without a version.
- **Lock files** (`packages.lock.json`) record exactly what each project restores. Commit them with any package change. CI restores in locked mode, so it fails if they're out of date. The macOS host has none, because its only packages come from the SDK and the macOS workload.
