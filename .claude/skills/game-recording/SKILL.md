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

The tools are the `stgadgets` CLI's `recording` commands (`docs/cli.md`): `record`, then `markers`, `search`, `history`, `values`, `mark` and `trim`. Run them as `bin/stgadgets recording <command>` (or `stgadgets` with direnv); below, `stgadgets` means that. Add `--help` to any command for every option.

## 1. Prerequisites

- RetroArch is running with `network_cmd_enable = "true"` (port 55355) and RetroAchievements hardcore mode off, with the game loaded.
- Check with: `printf GET_STATUS | nc -u -w1 127.0.0.1 55355`. The reply looks like `GET_STATUS PLAYING fb_alpha,invaders,crc32=…`: the core's system ID, then the ROM set name.
- **Work out what to record.** You need the RAM range, in the original hardware's addresses, and the core profile: where `READ_CORE_RAM` offset 0 lands (`ram_base`), how much it exposes, and whether words come back byte-swapped.
  - **Existing pack:** the recorder takes the core profile from `packs/<id>/providers/retroarch.yaml`. With no `--range`, it records what the pack's map reads: small, and enough to verify the map. To hunt for new addresses, give `--range` for the work RAM (the regions in `map.yaml`).
  - **New game:** look up the CPU's memory map (MAME driver source, a disassembly, or `docs/memory-maps.md` → finding addresses). Probe what the core exposes with `READ_CORE_RAM <hex offset> <len>`: find the readable size, and match the bytes against something known to place `ram_base`. If the core lays the RAM out differently from the hardware (check the core's driver source), give `segments` instead, so recordings come out in the hardware's addresses. If `READ_CORE_RAM` returns `-1`, this core can't be read. Say so and stop. Otherwise write the profile in the shape of a pack's `providers/retroarch.yaml` (`docs/packs.md` → provider settings) and pass it with `--settings`; once it's confirmed, put it in the pack.
- **Size matters.** At 60 ticks a second, each KB recorded costs about 3.6 MB per minute. Record only the work RAM you need, keep sessions to a few minutes, and `trim` afterwards.

## 2. Record while the user plays

Start the recorder **in the background**, then tell the user exactly what to do in the game:

```sh
stgadgets recording record packs/<id>/recordings/<name> --provider retroarch --range work_ram=0x2000:0x400 \
  --watch p1.score --watch 0x21FF:u8:spare_ships --seconds 300
```

- It prints how much it records a tick and per minute; check that before the user starts playing.
- `--watch` logs every change to `changes.log`, with tick numbers: a map variable's name, or `ADDR[:TYPE[:NAME]]` with a `map.yaml` type (`u8`, `u16`, `s16be`, `bcd2`, …) in the map's byte order. Watch whatever you already know or suspect.
- Give the user a short script of events that separate the value you're after, such as "insert a coin, start, lose one life, then wait". Ask them to tell you as each one happens.
- **Live markers:** when the user says something happened, run `echo "<label>" > packs/<id>/recordings/<name>/MARK`. Relaying through chat adds a delay of a few seconds, so treat these markers as approximate and refine them later with `history`.
- **Stopping:** `touch packs/<id>/recordings/<name>/STOP`, or let `--seconds` run out. It also stops if the game changes or RetroArch stops answering. The manifest is written on stop.
- When it stops it prints the time between snapshots. If `p50` is well above about 17 ms, or RetroArch stopped answering, mention it. The recording may be missing frames.

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
- **68000 games on FBNeo** (e.g. DoDonPachi): the core returns RAM byte-swapped. With `word_swapped: true` in the core profile (the pack's `providers/retroarch.yaml`, or the `--settings` file), the recorder stores it in the hardware's order.
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
