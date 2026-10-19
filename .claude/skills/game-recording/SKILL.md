---
name: game-recording
description: Record a game's RAM from RetroArch while the user plays, then use the recording to find or verify memory addresses, and to check a pack's map with expected values. Use when onboarding a new game pack, hunting for an address (score, lives, rank, chain…), checking a map against real play, or making a local test recording. Recordings are local and git-ignored.
---

# Game recordings

A **recording** is a game's RAM captured every frame while someone plays. The format is described in `docs/providers.md` (recording format). Use recordings to:

- **find addresses** for a new pack, cheat-finder style;
- **verify** a pack's `map.yaml` against real play;
- **test** a map: an `expected.yaml` next to a recording is checked by `dotnet test src/server` (`docs/packs.md`, Testing).

Recordings live in `packs/<id>/recordings/<name>/`, which is **git-ignored** (ADR 0011). They're local working material, not part of the repo. Don't try to commit them, and delete them when you're done if they're large.

Paths below are relative to the repo root.

- `.claude/skills/game-recording/scripts/record.py` captures a recording from RetroArch. It's plain Python (standard library only); always run it with `python3 -I`.
- The `stgadgets` CLI does everything else with a recording: `markers`, `search`, `history`, `values`, `mark` and `trim` (`docs/cli.md`). Run it with `dotnet run --project src/cli/Stgadgets.Cli -- recording <command>`; below, `stgadgets` is short for that.

Run either with `--help` for every option.

## 1. Prerequisites

- RetroArch is running with `network_cmd_enable = "true"` (port 55355) and RetroAchievements hardcore mode off, with the game loaded.
- Check with: `printf GET_STATUS | nc -u -w1 127.0.0.1 55355`. The reply looks like `GET_STATUS PLAYING fb_alpha,invaders,crc32=…`: the core's system ID, then the ROM set name.
- **Work out what to record.** You need the RAM range, in the original hardware's addresses, and where `READ_CORE_RAM` offset 0 lands in that range (the core's `ram_base`).
  - **Existing pack:** read `packs/<id>/providers/retroarch.yaml` (`cores.<system>.ram_base` / `ram_size`) and the regions in `map.yaml`.
  - **New game:** look up the CPU's memory map (MAME driver source, a disassembly, or `docs/memory-maps.md` → finding addresses). Probe what the core exposes with `READ_CORE_RAM <hex offset> <len>`: find the readable size, and match the bytes against something known to place `ram_base`. If `READ_CORE_RAM` returns `-1`, this core can't be read. Say so and stop.
- **Size matters.** At 60 ticks a second, each KB recorded costs about 3.6 MB per minute. Record only the work RAM you need, keep sessions to a few minutes, and `trim` afterwards.

## 2. Record while the user plays

Start the recorder **in the background**, then tell the user exactly what to do in the game:

```sh
python3 -I .claude/skills/game-recording/scripts/record.py \
  --out packs/<id>/recordings/<name> --base 0x2000 --size 0x400 --ram-base 0x2000 \
  --watch 0x20F8:bcd2:p1.score --watch 0x21FF:u8:p1.ships_reserve --seconds 300
```

- `--watch ADDR[:TYPE[:NAME]]` (types `u8`, `u16le`, `u16be`, `bcdN`, `bcdNbe`) logs every change to `changes.log`, with tick numbers. Watch whatever you already know or suspect.
- Give the user a short script of events that separate the value you're after, such as "insert a coin, start, lose one life, then wait". Ask them to tell you as each one happens.
- **Live markers:** when the user says something happened, run `echo "<label>" > packs/<id>/recordings/<name>/MARK`. Relaying through chat adds a delay of a few seconds, so treat these markers as approximate and refine them later with `history`.
- **Stopping:** `touch packs/<id>/recordings/<name>/STOP`, or let `--seconds` run out. The manifest is written on stop.
- If the log shows `round-trip p50` well above about 17 ms, or RetroArch stopped answering, mention it. The recording may be missing frames.

## 3. Find addresses

Use `search` to narrow down candidates. Each filter compares one byte between two points: tick numbers, `start`, `end`, or marker labels (exact or a unique prefix). Filters apply in order.

```sh
stgadgets recording markers REC
stgadgets recording search REC --decreased "1st death" "2nd death" --decreased "2nd death" "3rd death" --equals end 0
stgadgets recording search REC --changed start "coin" --unchanged "coin" "start pressed"
```

Tips:

- Combine "changed when X happened" with "unchanged when nothing relevant happened". The second kind of filter removes most of the noise.
- Values are often stored differently from how they're shown: lives as spare ships (shown − 1), scores as BCD without the last digit, flags as single bits. Try `--equals` with a few variations.
- 16-bit values show up as two neighbouring bytes, and on the 68000 the high byte comes first. `history` reads an address with `--type` as `map.yaml` does (`u16`, `s16be`, `bcd2`, …), in the pack map's byte order; for a recording not in a pack yet, add `--endianness big` for 68000 games.
- **68000 games on FBNeo** (e.g. DoDonPachi): the core returns RAM byte-swapped, so record with `--word-swapped` to store it in the hardware's order. The pack's `providers/retroarch.yaml` says `word_swapped: true` when this applies.
- Confirm a candidate with `stgadgets recording history REC 0xADDR --type bcd2`. Its distinct values over time should match what happened on screen.
- Once the address is in the map, `history REC <variable or derived value>` shows it through the pack: `history REC p1.lives`.
- Use `stgadgets recording mark REC <tick> "<label>"` to pin events exactly once `history` shows the tick where something changed.

For hard cases (rank, hidden timers), suggest the MAME debugger with watchpoints (`docs/memory-maps.md` → finding addresses).

## 4. Verify a map and write expected values

1. Add or fix the variables in `packs/<id>/map.yaml`, and set `verified: true` on the long-form ones you've confirmed.
2. Write `packs/<id>/recordings/<name>/expected.yaml`. Use `at:` to give values at `start`, tick numbers or marker labels, and use the tick before a marker for "just before". Use `sequences:` to give every distinct value in order. The format is in `docs/packs.md` → Testing. Build it from `history` output, not from memory.
3. Run `dotnet test src/server`. `PackRecordingTests` checks every local recording, and prints each mismatch.
   To see every decoded and derived value at a point (to check `derived.js`, or to start a game state or scenario from it), use `stgadgets recording values REC --at "<marker>"`.
4. To keep a recording small enough to hold onto, use `stgadgets recording trim REC <first> <last> --out <new>`. That keeps markers and rebases timestamps.

## 5. Report back

Tell the user which addresses were confirmed and how. Mention anything still uncertain, the recording's location and size, and whether it's worth keeping. Recordings never go in git; if one is worth sharing, the user can share it outside the repo.
