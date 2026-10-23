# Roadmap

## Principles

- **Get the whole path working first.** Make one game, one variable and one gadget work from emulator to overlay before generalising anything. Map format features, standard names, layout packaging and so on get added when a real game needs them, not before.
- **Find the risks early.** Each milestone starts with whatever is most likely to sink it.
- **Measure latency from day one.** See [requirements](requirements.md#latency-highest-priority).

Space Invaders (Taito, 1978) is a good first game: it has an 8-bit CPU (Intel 8080), 1 KB of work RAM, a well-documented disassembly, an on-screen score we can use for latency tests, and a rotated monitor.

## 0. Spike

The riskiest assumption is that we can read Space Invaders' memory, at the original hardware's addresses, on macOS. Test both candidates before writing anything for real:

- [x] **RetroArch:** turn on network commands, load Space Invaders in FBNeo (then the MAME cores), and try `GET_STATUS` and `READ_CORE_MEMORY 2000 400` with `nc -u`. Is the memory exposed? At which addresses? How long does a round-trip take?
- [ ] ~~**MAME:**~~ not needed for now. RetroArch worked ([ADR 0009](decisions/0009-first-provider-retroarch.md)). Kept as the second provider. A minimal Lua plugin that reads `0x2000–0x23FF` in `register_frame_done` and writes it out every frame.
- [x] Write up the result as ADR 0009: the first provider.

**Done when** we have bytes we trust from Space Invaders' work RAM, every frame, and a decision on the first provider.

## 1. Thinnest end-to-end path

One game, one variable, one gadget, on screen, with latency measured.

- [x] Set up the .NET solution (`src/`, `tests/`) on .NET 10
- [x] The chosen provider, streaming work RAM
- [x] Capture recordings: `stgadgets recording record` ([cli.md](cli.md)), used by the `game-recording` skill ([ADR 0011](decisions/0011-recordings-are-local.md)) in the [recording format](providers.md#recording-format-draft)
- [x] Capture fixture recordings of attract mode, scoring, the mystery ship, losing a life and game over (still to capture: inserting a coin, clearing a wave)
- [x] Check the draft addresses in [memory-maps.md](memory-maps.md) against the recordings
- [x] Minimal decoding of `p1.score` (BCD), with the map hardcoded or very minimal
- [x] Kestrel WebSocket server and a bare `@stgadgets/sdk` with `useVar`
- [x] A score gadget in a browser
- [x] Spike: overlay window on macOS ([ADR 0007](decisions/0007-native-overlay-host.md)) showing the gadget over the emulator (works in fullscreen too)
- [x] Pipeline timestamps and a latency debug gadget (read → paint; full per-stage timestamps still to do)
- [x] End-to-end latency test: screen recording, game score vs gadget score (`stgadgets latency analyse`, run by the `latency-test` skill). Passes fullscreen at 60 Hz ([latency.md](latency.md)); windowed still to run
- [ ] Check L1: emulator latency with and without the overlay. Parked: RetroArch is composited with or without the overlay, so it's unlikely to add lag ([latency.md](latency.md#2026-10-20-presentation-mode-with-and-without-the-overlay-l1)). Still to do: a setup where RetroArch gets direct presentation, a camera test if the overlay breaks it, and CPU use in Release builds

**Done when** the score gadget follows the game's score in the overlay within the [L2 targets](requirements.md#l2-the-overlay-must-keep-up-with-the-game), and the overlay adds no measurable lag to the emulator.

## 2. Turn it into a platform

Generalise only what milestone 1 proved:

- [x] Map loader and decoder for real (YAML, types, byte order)
- [x] Match maps to ROM set and chip CRCs (RetroArch finds the zip through its content history; console ROM hashes wait on a console pack)
- [x] Replay provider ([providers](providers.md#replay-provider))
- [x] Decode a recording and print the variables: `stgadgets recording values` and `history` ([cli.md](cli.md))
- [x] Unit tests for decoding against the recordings
- [x] SDK: `useGame`, `useConnection`, `useOnFrame`
- [x] Gadgets: lives, aliens remaining. Shots fired and accuracy (as derived values, [ADR 0012](decisions/0012-derived-values-in-javascript.md))
- [x] Overlay follows the emulator window; menu bar app

**Done when** a live score and accuracy panel sit next to a game of Space Invaders and stay correct, and the same layout runs in a browser against a recording.

## 3. Prove out the platform with additional packs

Grow the platform by adding packs, and pick each game for what it needs that the platform doesn't do yet. Games with hidden systems are where ShotTriggers-style gadgets earn their keep. Each pack's open work, such as states not yet checked, clone sets and gadget ideas, lives in its own `NOTES.md`, not here.

**Done when** a new pack can be written without changes to the platform.

### Map authoring tools

Tools to find addresses (see [memory-maps.md](memory-maps.md#finding-addresses)):

- [x] RAM search on recordings (changed, unchanged, went up, went down, equals a value): `stgadgets recording search` ([cli.md](cli.md)), used by the `game-recording` skill
- [ ] Watch view: show chosen addresses live while playing (for now, `recording record --watch` logs them to `changes.log`)
- [ ] Import helpers for MAME cheat files

### Map format

- [x] 16-bit values and big-endian CPUs (68000), including FBNeo's byte-swapped 68000 RAM (`word_swapped` in a pack's `providers/retroarch.yaml`)
- [x] Bit fields in maps (`mask`, `shift`)
- [x] Arrays (enemy and bullet tables): `count` entries `stride` bytes apart, each with the same fields
- [ ] Pointers
- [ ] Maps that extend other maps, for clone ROM sets
- [ ] Calibrating the game picture position, and `GameSpace`

### Providers

- [ ] Second provider: MAME, kept from milestone 0

### Gadgets for hidden systems

- [x] **Rank meters:** hidden difficulty that changes as you play, its parts and what changed it
- [x] **Chain and combo systems**
- [ ] **Hitbox overlays:** on hold, undecided whether gadgets should draw over the game at all. They'd also need exact alignment with the game picture, per-object position data, and a canvas renderer

## 4. Single distributable

Turn stgadgets from something that runs from this repo into one thing people can install and run on macOS, Windows
and Linux.

**Done when** someone who has never seen this repo can install stgadgets on any of the three, point it at RetroArch
and see a pack's layout over their game.

### Windows host

- [ ] Overlay host for Windows (separate session): a transparent, click-through window with WebView2, sharing
  `host/lib`, including the L1 check for independent flip
- [ ] The same development hooks as the macOS host, such as snapshots

### Linux host

- [ ] Decide how to support X11 and Wayland: on Wayland, a click-through window that stays above another app's window
  depends on the compositor
- [ ] Overlay host for Linux: a transparent, click-through window with WebKitGTK, sharing `host/lib`, including the L1
  check
- [ ] The same development hooks as the macOS host, such as snapshots

### Build tooling and a single package

- [ ] One build that produces a release for each OS: the server (self-contained .NET), the UI, the built-in packs and
  the overlay host, as one installable package (a `.app`/`.dmg` on macOS, an installer on Windows, and a package for
  Linux, format to decide)
- [ ] The host starts and stops the server itself, so users run one thing
- [ ] Signing and notarization (Apple Developer account for macOS; code signing on Windows)
- [ ] CI that builds and tests each OS's package

### User documentation

- [ ] User docs: installing, setting up the emulator (RetroArch network commands, cores), the overlay and OBS
- [ ] A list of supported games and emulator cores, with what each pack shows

## 5. Ready for platform developers

Let people write and share packs without this repo.

### Pack test framework

Recordings stay local ([ADR 0011](decisions/0011-recordings-are-local.md)), so their checks only run on the machine
that made them. Committed scenarios and layout tests now cover each pack's logic and UI wherever it's checked out.

- [x] Committed tests for a pack's logic that don't need a recording: scenarios of hand-written ticks of map values
  in, expected derived values out, in the pack's `tests/derived/` ([packs.md](packs.md#testing-derived-values))
- [x] The `stgadgets` CLI (`src/cli/`), on `Stgadgets.Core` and the providers moved to `src/lib/`
  ([ADR 0015](decisions/0015-pack-tools-in-a-dotnet-cli.md)): `recording record`, `markers`, `search`, `history`,
  `values`, `mark` and `trim`, replacing the `game-recording` skill's Python scripts ([cli.md](cli.md))
- [ ] `stgadgets pack check <dir>`: check a pack against the schema (pack.yaml, map.yaml, derived.js, and the UI's
  imports)
- [ ] `stgadgets pack test <dir>`: the scenario and recording checks that `dotnet test src/server` runs today, for a
  pack anywhere on disk
- [x] Testing a pack's layout: show it in named game states and check what's on screen, without snapshots
  ([packs.md](packs.md#testing-the-ui))

### Documentation

- [ ] A pack author's guide, from [developing a game pack](pack-development.md), with a reference for the map
  format, `derived.js`, the SDK and the standard gadgets that doesn't assume this repo

### Packs outside the repo

- [ ] Load packs from a user folder as well as the built-in ones, with a rule for which wins when both have a game
- [ ] Build a pack's UI separately and load it at runtime as an ES module, with the SDK, gadgets and React shared
  through an import map ([gadgets](gadgets.md#open-questions))
- [ ] Versioning: which platform and SDK versions a pack works with
- [ ] Trust and sandboxing for third-party `derived.js` and UI code: its own ADR, as
  [ADR 0010](decisions/0010-game-packs.md) planned

## Dev env improvements

Found while building the DoDonPachi pack:

- [x] Overlay snapshots: `STGADGETS_SNAPSHOT_PORT` makes the overlay serve a PNG of the layout, so a change can be checked without looking at the screen ([pack development](pack-development.md#the-build-and-look-loop))
- [x] Test game states: a pack's `tests/states.ts`, written as ticks of map values with `derived.js` computing the rest, shown with `/?state=` in the dev server and screenshotted with `npm run screenshot -- --state`, so a layout can be seen at any moment of the game without playing to it ([pack development](pack-development.md#looking-at-game-states))
- [x] The server's emulator is chosen in configuration (`Stgadgets:Provider`), from the same list of providers as the CLI ([providers](providers.md#choosing-a-provider))
- [x] Replay provider (milestone 2), so a layout can be viewed against a recording, not only live play: `Stgadgets:Provider=replay`, with a recording's folder as the endpoint, optionally `#<marker>` to hold at a moment ([providers](providers.md#replay-provider))
- [x] `stgadgets dev`: start, restart, check and stop the server (live or replaying) and the overlay, and take snapshots, in one command each ([cli.md](cli.md#development))
- [x] CI: a GitHub Actions workflow (`.github/workflows/ci.yml`) that builds every module in parallel and runs the committed tests and linting on every push to `main` and every pull request: the server and CLI with their tests, the macOS overlay, and the UI with its tests, Oxlint and Oxfmt. No emulator is needed, and recordings are local, so their checks are skipped there ([conventions](conventions.md#linting))
- [x] A tool to show a pack's derived values over a recording, for working out `expected.yaml` and debugging `derived.js`: `stgadgets recording history <name>` and `values` ([cli.md](cli.md))
- [ ] A gadget interview: a guided conversation about what a game's players would want to see, drawing on existing packs and the game's own systems, ending in a list of gadgets to build and the data each needs ([choosing gadgets](pack-development.md#2-decide-which-gadgets-are-worth-having))
- [ ] `/add-pack <game>`: one Claude skill that runs the whole workflow (research, recording, map, derived values, gadget interview, gadgets, game states and tests), built from the pieces above
- [ ] Hot reload packs in the server: watch `packs/` and reload the matched pack when its `pack.yaml`, `map.yaml`, `derived.js` or provider settings change, so a `derived.js` change shows up in the overlay without restarting the server
- [ ] Keep derived state, such as stage splits, across reloads and server restarts: today a restart resets the splits
- [ ] Find why an overlay can get stuck painting at half the display's rate until it's restarted ([latency.md](latency.md#2026-10-20-end-to-end-test-l2))
- [ ] Say in the docs and UI that RetroArch's run-ahead puts the overlay N frames behind the picture
- [ ] Show the read → paint latency meter as idle while the overlay is hidden: a hidden overlay doesn't paint, so its numbers stop meaning anything

## Any time

- Input display gadget. Needs its own input source: reading the controller directly (GameController/HID on macOS) or the game's input bytes in RAM. Emulator APIs don't give us controller input.
- Using a layout as an OBS browser source
- Distribution: see [milestone 4](#4-single-distributable)
