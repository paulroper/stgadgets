# DoDonPachi research notes

How DoDonPachi works, as far as stgadgets has found out: where `map.yaml`'s addresses come from, the rules
`derived.js` follows, and what's still open. Addresses are the 68000's, and code addresses are the World set's
(`ddonpach`), unless noted.

## Sources

| Source | What it gave | Notes |
|---|---|---|
| [epozzobon's RE notes](https://epozzobon.it/re/ddonpach/) | Rank formula and its inputs, hitboxes (`0x102C9C`/`0x102CDC`), slowdown and RNG behaviour | `ddonpachj`. Public domain |
| [Grego and alamone's trainer](https://github.com/originalgrego/ddonpachj-trainer) (`ddonpachj_hack.asm`) | Lives, stage, loop, bombs and capacity, shot/laser power, invincibility, credits, frame counter, max bonus | `ddonpachj`. GPL-3.0; only addresses are used here |
| [RetroAchievements code notes, game 12751](https://retroachievements.org/game/12751) | Scores, chain (hit timer, hits, max hits), game state, stage progress, deaths, bees, stars, DIP settings, boss positions | Public `codenotes2` request, no API key needed. Addresses are FBNeo's: offset 0 is `0x100000` |
| [shmups wiki](https://shmups.wiki/library/DoDonPachi) | Second loop conditions, bees, power-ups, chain scoring, MAXIMUM bonus, stage clear bonuses | Game rules, checked against the code below |
| [MAME `cave.cpp`](https://github.com/mamedev/mame/blob/master/src/mame/atlus/cave.cpp) | Memory map (RAM `0x100000-0x10FFFF`), ROM sets and CRCs, refresh rate | |
| World program ROMs (`b1.u27`/`b2.u26`), disassembled with Capstone | Checked every address above is used by the World code, and read the routines quoted here | Not in the repo |

The RetroAchievements notes are community-written with no stated licence. We only take addresses (facts), credit
them, and don't copy achievement logic.

## Reading the game's memory

### World vs Japan

Every address from the `ddonpachj` sources is referenced in the World ROMs, often by the same code at the same
place: the rank routine is at `0x3362` in both. The code shifts in places (the trainer's CRC-skip patch is at
`0x5410` in Japan and `0x5404` in World), and some data differs (the rank tables, below), but the RAM layout looks
the same. So `ddonpachj` can probably join this pack once it's been checked.

### FBNeo swaps the bytes of each word

FBNeo keeps 68000 RAM as 16-bit words in the host's byte order, so on a little-endian machine each pair of bytes is
swapped. `READ_CORE_RAM` returns them that way (ship type B reads `01 00` at offset `0x2CA8`), so
`providers/retroarch.yaml` sets `word_swapped: true` and the provider swaps them back. Offset 0 is `0x100000`.

RetroAchievements' notes are in FBNeo's layout too. Their 16-bit notes come out right, but their **8-bit notes point
at the other byte of the word**: RA `0x2CB0` "bomb slots" is the 68000's `0x102CB1`, and RA `0x2CB1` "bombs" is
`0x102CB0`. The World code confirms this (the bomb pickup at `0x4EC8A` increments `0x102CB0` up to `0x102CB1`), and so
does the trainer. To convert an RA 8-bit address: add `0x100000`, then flip bit 0.

### Values stored as words

Many values are 16-bit words whose meaning is in the low byte, at the odd address: rank is written as a byte at
`0x101969`, the rank routine reads lives from `0x101965`/`0x101967`, and the settings byte is `0x101299`.

### Leftovers outside play

The last game's score, power and other values stay in memory through the title and ship select screens (power reads
3 on ship select, and for the first frame of stage 1). `game.running` (`game.state` 0, 6 or 7) gates everything in
`derived.js`.

## Game flow

`game.state` (`0x1017A6`): 0x0C "presented by Cave", 0x0E briefly, 1 title, 4 title with a credit in, 0x0B ship
select, 7 play with P1 (also the stage results and ending), 8 continue. From RA, not yet seen: 0 play with both
players, 2 score ranking, 5 demo, 6 play with P2. `game.phase` (`0x1017A4`, RA: 3 results, 4 ending) stayed 0
throughout.

`game.flags` (`0x1017A9`) bit 6 is needed for the second loop; it was set throughout a solo game (0xE0 in play, 0xF0
on the continue screen).

## Settings (DIP switches)

The settings byte is `0x101299`, copied to `0x101294` each frame (`0x9C0`) for the code to read:

| Bits | Setting | Values | Source |
|---|---|---|---|
| 0-1 | Difficulty | 0 normal, 1 easy, 2 hard, 3 very hard | Code (`0x5024` picks the rank table) |
| 2-3 | Extends | 0: 6M then 20M; 1: 15M then 30M; 2: every 10M; 3: none | Code (`0x44CCA`, tables at `0x44CA0` and `0x44CDA`) |
| 4-5 | Lives | 0: 3, 1: 5, 2: 2, 3: 1 | RA |
| 7 | Continue | 0 on, 1 off | RA |

Only the defaults (all 0) have been seen in play.

## Score

- **9 digits.** Memory holds the first 8 as BCD (`p1.score_digits`, `0x10161E`); the last digit shown is the
  continues used, the low byte of `p1.continues` (`0x10174E`, `0x100` + continues). `derived.js` puts them together.
- **The score only changes every other frame,** so a hit's points can land on the tick after it.
- **Chains:** each kill scores its base value × the chain's hit count, e.g. +1,400 for the 14th hit on a 100-point
  enemy. Smaller gains (+80 every so often, +10s) came without a hit; their sources aren't pinned down.
- **High score:** the score ranking is 5 entries of 8 BCD digits at `0x1016EA` (searched by `0x431A4`), with each
  entry's last digit in a table of words at `0x101744`. The top entry is the high score shown.

## Lives and extends

- `p1.stock` (`0x101964`) counts the ship in play, and only drops when the ship respawns, about 220 frames after
  the hit. `p1.flags` bit 0 marks the explosion, so `derived.js` takes the exploding ship off for the standard
  `p1.lives`.
- **Extends:** `p1.next_extend` (`0x101950`, P2 `0x101954`) holds the score for the next life, as BCD score digits,
  or `0xFFFF` once there are none. When the score passes it, `0x44BBE` adds a life and `0x44CEA` moves the target on
  by the setting's step (a step with bit 31 set repeats, for "every 10M").
- **After clearing 2-7:** 10,000,000 points for each life left (`0x12E6`).

## Player block

0x40 bytes per player, P1 from `0x102C8C`, P2 from `0x102CCC`:

| Offset | P1 | Meaning |
|---|---|---|
| +0x00 | `0x102C8C` | `pN.flags`: bit 7 ship in play; bit 0 hit and exploding (bit 7 clears), bit 2 joins it ~40 frames later; all clear for one frame at respawn; bit 6 set by using a bomb, until death; bit 3 flickers, cause unknown |
| +0x10 | `0x102C9C` | Hitbox (epozzobon): 7×6, 7×4 while moving sideways |
| +0x1C | `0x102CA8` | Ship: 0 A, 1 B, 2 C |
| +0x1E | `0x102CAA` | Style: 0 shot (S), 1 laser (L) |
| +0x20 | `0x102CAC` | Shot power, 0-4 |
| +0x22 | `0x102CAE` | Laser power, 0-4 |
| +0x24 | `0x102CB0` | Bombs in stock |
| +0x25 | `0x102CB1` | Bomb capacity |
| +0x3E | `0x102CCA` | Invincibility after respawning, from 0xF0 down to 0 |

## Power and bombs

- **Power-ups** raise shot and laser power together; 4 is max. On death, the power of your style's weapon drops by
  one and the other's resets (seen: shot 3, laser 0 after a death at higher power). After a continue, both are 4.
- **Bombs:** type B starts with 3 of 3. Each death adds a slot, and the stock refills to capacity on respawn (3 → 4 →
  5 → 6). After a continue: 6 of 6. A pickup adds 1 unless the stock is full (`0x4EC8A`).
- **MAXIMUM bonus:** a pickup that fills the stock adds `0x16` to `0x101924` (P1); 0x16 score digits is 220 points,
  the wiki's "220 × multiplier" per frame, so this looks like the per-frame bonus. A pickup with the stock already
  full goes to `0x4ED8E`, not yet traced. `p1.max_bonus` (`0x10192A`) is RA's MAXIMUM value. Not checked in play.

## Chain

- `p1.hit_timer` (`0x1017D0`) is the chain gauge: it counts down by 1 a frame, and each hit tops it up, to 55 at
  most (`chain.timer_reload` is 56). The chain is over at 0.
- `p1.hits` (`0x1017EA`, BCD) keeps its value after the gauge runs out, until the next hit starts a new chain, so
  `derived.js` gives `p1.chain` as 0 once it's over.
- `p1.max_hits` (`0x101842`, BCD) is the longest chain this credit; a continue resets it.
- The reload value comes from a table at `0x440E2`; the wiki says the laser's hit countdown depends on its level
  (62, 56, 50, 44, 38 frames), which may be the same table.

## Rank

From the World code at `0x3362`, which epozzobon's formula matches, with some extra details:

- The players counted come from `0x5A688`: bit 7 set and bit 0 clear in `pN.flags`. So **while your ship explodes,
  your own terms drop out** until respawn.
- Per player: `ship + 2 - 2 * style + 2 * shot_power + laser_power`, read with one `movem.w` from `0x102CA8`.
- The stage index is added for a player only while `pN.continues` is `0x100` (first credit), and again if
  `pN.deaths_stage` is 0.
- The sum is halved when both players are counted.
- Then: `+ table[stage]` (the difficulty's table, pointer at `0x10122E`), `+ 2 * (P1 lives + P2 lives)` (low
  bytes), `+ bees` (low byte of `0x103F8E`), `+ survival_frames >> 11`, `+ 32 * loop`, capped at 63.
- Afterwards: `0x10196B = rank / 2`, `0x10196D = rank / 4`, `0x10196F = max(rank - 32, 0) * 2`.

**The World set's rank tables** (pointers at `0x5092`) differ from epozzobon's `ddonpachj` ones on easy and very
hard:

| Difficulty | Stages 1-8 |
|---|---|
| Normal | 16, 20, 24, 28, 32, 36, 40, 44 |
| Easy | all 0 |
| Hard | 32, 38, 44, 50, 56, 62, 68, 74 |
| Very hard | 63 ×7, then 0: rank is always at its cap |

**Rank is only recalculated as the stage scrolls** (each step of `stage.progress`, `0x3326`). The scroll stops for
bosses and for 2-3 seconds at times mid-stage, and rank stays put meanwhile, so **rank is frozen for a whole boss
fight**. `derived.js` computes the formula's parts (`rank.from_*`) and their total (`rank.model`); over a whole
recorded session the model matched `rank` except while waiting for the next scroll step. The rank gadget shows the
game's value, the model's reason for each change, and where rank is heading only while the stage scrolls.

The survival part adds 1 rank every 2048 frames (about 34 s) without being hit. `rank.survival_frames` is reset the
moment the ship is hit, and starts counting again about 100 frames before the ship respawns.

## Bees

- 13 per stage. `bees.stage` (`0x103F8E`) counts them, and **resets on death**; the bees already taken don't come
  back, so a perfect stage means all 13 without dying after the first.
- The 13th (`0x4D2D2`: the count was 12 before the pickup) adds one to `bees.perfect_stages` (`0x1017C2`).
- A bee is worth `table[bees this stage + perfect stages so far]` from `0x4D3A4`, capped at the last entry: 100,
  200, 400, 800, 1,000, 2,000, 4,000, 8,000, 10,000, 20,000, 40,000, 80,000, 100,000, then 200,000 up to 1,000,000.
  This matches the wiki.

## Second loop

The check at `0x400B4` matches the shmups wiki: P1 in play, `game.flags` bit 6 (solo), first credit (`p1.continues`
`0x100`), then any of:

- `p1.deaths_first_credit` (`0x101948`) at most 2. It stops counting on the final death, and a continue resets it,
  but by then the credit doesn't qualify anyway.
- `p1.max_hits` at least 270, 300 or 330 for ship A, B or C (BCD table at `0x40038`).
- `p1.score_digits` at least `0x05000000` (50,000,000 points).
- `bees.perfect_stages` at least 4.

Not yet checked by reaching the end of 1-6.

## Stage clear bonus

The results code (`0xF24`, `0x10CA`) pays:

- 1,000 points per star counted at `0x103F90` and 500 per star at `0x103F92` (RA calls them ground and aerial
  stars), up to 999 each. Both reset on death.
- The no-miss bonus, if `pN.deaths_stage` is 0: 200,000 to 700,000 for 1-1 to 1-6, and 2,000,000 to 8,000,000 for
  2-1 to 2-7 (tables at `0x14C8`). The wiki says loop 2 goes up to 7,000,000, but the ROM's table has 8,000,000 for
  2-7; unchecked in play.

`pN.deaths_stage` (`0x10194C`) goes up by 2 per death (`0x6E08`), at the hit.

## Stage progress and the boss

`stage.progress` (`0x10198E`) is a signed scroll counter: -41 as stage 1 starts, then up 1 per scroll step (every
frame during the fly-in, about every 16 frames after). RA gives the bosses at 0x200, 0x160, 0x1B0, 0x200, 0x250 and
0x121 for stages 1-6. During the stage 1 boss fight, progress was at or past 0x200; the others are unchecked, and
stage 7's isn't known.
The scroll speed isn't steady. In stage 1, progress went from -40 to 208 at 1 step a frame (about 4 s, 40% of the way
to the boss), slowed through 2, 4 and 8 frames a step, then cruised at 1 step every 16 frames from 240 on, with stops
of 2-3 s at 264, 291, 311 and 339. Other stages are unchecked. `boss.eta` uses the cruising speed: it falls fast
through the opening burst, then counts down in real time and holds during stops (which it can't foresee). An earlier
version averaged the recent step rate, but the opening burst inflated that average, so the estimate barely moved.

## Slowdown

`frame_counter` (`0x101270`) goes up once per pass of the main loop, which waits for vblank, so a pass that runs
long skips a frame (epozzobon's notes explain why: more objects, transparency on odd frames, P1's collisions on odd
frames). The board refreshes at 15625 / 271.5 = 57.55 Hz (MAME `cave.cpp`), and RetroArch polled at 57.59 a
second, so each tick is one emulated frame. The counter advanced on every tick on the ship select screen and early
in stage 1, but on only 70-89% of them later in stage 1.

`game.speed` is that share, smoothed over about half a second. Runs of more than 30 ticks without the counter
moving are treated as a pause (the game's or the emulator's) and left out of both the speed and the splits.

## Performance

`derived.js` has about 90 values and costs about 0.33 ms per tick, well inside a frame. One derived value per stage
per split field (65 values) was too much to load within the time limit, so the finished splits are kept as one JSON
string (`split.done`), with live counters for the current stage only.

## Future ideas

- **Hitbox overlay** (`0x102C9C`, 7×6 shrinking to 7×4 while moving sideways). On hold: drawing over the game feels
  too close to cheating, so the gadgets stay beside it for now.
- **Rank graph:** rank over the whole run, with markers at deaths and bombs, from `rank` and `p1.died`.
- **Personal-best splits:** compare `split.*` with a saved best run. Needs somewhere to keep it between sessions.

## Still to find or check

- P2's variables, `game.state` 0 and 6, and the two-player rank halving: a two-player session would check them.
- `game.state` on the results and ending screens, then whether `game.running` should exclude them.
- The non-default settings (difficulty, extends, lives), the second extend, and the second loop.
- The MAXIMUM bonus (`0x101924`, `0x10192A`), and the second star counter (`0x103F92`).
- The boss positions for stages 2-6, and stage 7's.
- `p1.flags` bit 3, and what the word at `0x10184C` (P1) / `0x101850` (P2) is: it keeps the higher of itself and the
  score when a ranking entry is made (`0x5272`).
- The object table (RA: 64-byte entries from `0x1001CE`), e.g. for boss health. Needs arrays in the map format.
- `ddonpachj` (Japan) and `ddonpacha` (the 2012 Arrange hack), to add them to the pack. The trainer patches
  `ddonpachj`, so its stage select only helps once `ddonpachj` is in.
