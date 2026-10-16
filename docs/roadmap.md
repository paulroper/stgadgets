# Roadmap

## Principles

- **Get the whole path working first.** Make one game, one variable and one gadget work from emulator to overlay before generalising anything. Map format features, standard names, layout packaging and so on get added when a real game needs them, not before.
- **Find the risks early.** Each milestone starts with whatever is most likely to sink it.
- **Measure latency from day one.** See [requirements](requirements.md#latency-highest-priority).

Space Invaders (Taito, 1978) is a good first game: it has an 8-bit CPU (Intel 8080), 1 KB of work RAM, a well-documented disassembly, an on-screen score we can use for latency tests, and a rotated monitor.

## 0. Pick the first provider (spike, about a day)

The riskiest assumption is that we can read Space Invaders' memory, at the original hardware's addresses, on macOS. Test both candidates before writing anything for real:

- [x] **RetroArch:** turn on network commands, load Space Invaders in FBNeo (then the MAME cores), and try `GET_STATUS` and `READ_CORE_MEMORY 2000 400` with `nc -u`. Is the memory exposed? At which addresses? How long does a round-trip take?
- [ ] ~~**MAME:**~~ not needed for now. RetroArch worked ([ADR 0009](decisions/0009-first-provider-retroarch.md)). Kept as the second provider. A minimal Lua plugin that reads `0x2000–0x23FF` in `register_frame_done` and writes it out every frame.
- [x] Write up the result as ADR 0009: the first provider.

**Done when** we have bytes we trust from Space Invaders' work RAM, every frame, and a decision on the first provider.

## 1. Thinnest end-to-end path

One game, one variable, one gadget, on screen, with latency measured.

- [x] Set up the .NET solution (`src/`, `tests/`) on .NET 10
- [x] The chosen provider, streaming work RAM
- [x] Capture recordings: `record.py` in the `game-recording` skill ([ADR 0011](decisions/0011-recordings-are-local.md)) in the [recording format](providers.md#recording-format-draft)
- [x] Capture fixture recordings (`invaders/play-01`: mystery ship, deaths, game over; still to capture: inserting a coin, clearing a wave): attract mode, inserting a coin, scoring, losing a life, clearing a wave
- [x] Check the draft addresses in [memory-maps.md](memory-maps.md) against the recordings
- [x] Minimal decoding of `p1.score` (BCD), with the map hardcoded or very minimal
- [x] Kestrel WebSocket server and a bare `@stgadgets/sdk` with `useVar`
- [x] A score gadget in a browser
- [x] Spike: overlay window on macOS ([ADR 0007](decisions/0007-native-overlay-host.md)) showing the gadget over the emulator (works in fullscreen too)
- [x] Pipeline timestamps and a latency debug gadget (read → paint; full per-stage timestamps still to do)
- [ ] End-to-end latency test: screen recording, game score vs gadget score
- [ ] Check L1: emulator latency with and without the overlay

**Done when** the score gadget follows the game's score in the overlay within the [L2 targets](requirements.md#l2-the-overlay-must-keep-up-with-the-game), and the overlay adds no measurable lag to the emulator.

## 2. Turn it into a platform

Generalise only what milestone 1 proved:

- [x] Map loader and decoder for real (YAML, types, byte order)
- [ ] Match maps to ROM set and hash
- [ ] Replay provider
- [ ] `stgadgets map test`: decode a recording and print the variables
- [x] Unit tests for decoding against the recordings
- [ ] SDK: `useGame`, `useConnection`, `useOnFrame`
- [ ] Gadgets: lives, aliens remaining. Tracker: shots fired and accuracy
- [ ] Overlay follows the emulator window; menu bar app

**Done when** a live score and accuracy panel sit next to a game of Space Invaders and stay correct, and the same layout runs in a browser against a recording.

## 3. Map authoring tools

Before tackling harder games, build tools to find addresses (see [memory-maps.md](memory-maps.md#finding-addresses)):

- [x] RAM search on recordings (changed, unchanged, went up, went down, equals a value): `recording.py search` in the `game-recording` skill
- [ ] Watch view: show chosen addresses live while playing
- [ ] Import helpers for MAME cheat files

## 4. Fill out the format (early 80s and 16-bit era)

Pick games that need features Space Invaders didn't:

- 16-bit values and big-endian CPUs (68000)
- Arrays and structs (enemy and bullet tables), and pointers
- Maps that extend other maps, for clone ROM sets
- Calibrating the game picture position, and `GameSpace`
- Second provider (whichever wasn't picked in milestone 0)
- Windows overlay host (separate session), including the L1 check for independent flip

## 5. 90s shmups

Games with hidden systems are where ShotTriggers-style gadgets earn their keep:

- **Rank meters:** hidden difficulty that changes as you play
- **Chain and combo systems**
- **Hitbox overlays:** need exact alignment with the game picture, per-object position data, and a canvas renderer

## 6. DoDonPachi 🐝

The long-term goal. DoDonPachi (`ddonpach`, Cave, 1997) runs on a 68000-based Cave board, is vertical, and has a combo system that's an obvious fit for gadgets: a chain gauge, hit counter and hyper stock, and possibly bullet and hitbox overlays.

## Any time

- Input display gadget. Needs its own input source: reading the controller directly (GameController/HID on macOS) or the game's input bytes in RAM. Emulator APIs don't give us controller input.
- Using a layout as an OBS browser source
- Distribution: Apple Developer account, signing and notarization for macOS releases
