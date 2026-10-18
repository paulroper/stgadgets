# DoDonPachi research notes

Where `map.yaml`'s addresses come from, and what's still open. Addresses are the 68000's, in the World set
(`ddonpach`) unless noted.

## Sources

| Source | What it gave | Notes |
|---|---|---|
| [epozzobon's RE notes](https://epozzobon.it/re/ddonpach/) | Rank formula and its inputs, hitboxes (`0x102C9C`/`0x102CDC`), slowdown and RNG behaviour | `ddonpachj`. Public domain |
| [Grego and alamone's trainer](https://github.com/originalgrego/ddonpachj-trainer) (`ddonpachj_hack.asm`) | Lives, stage, loop, bombs and capacity, shot/laser power, invincibility, credits, frame counter, max bonus | `ddonpachj`. GPL-3.0; only addresses are used here |
| [RetroAchievements code notes, game 12751](https://retroachievements.org/game/12751) | Scores, chain (hit timer, hits, max hits), game state, stage progress, deaths, bees, DIP settings | Public `codenotes2` request, no API key needed. Addresses are FBNeo's: offset 0 is `0x100000` |
| [MAME `cave.cpp`](https://github.com/mamedev/mame/blob/master/src/mame/atlus/cave.cpp) | Memory map (RAM `0x100000-0x10FFFF`), ROM sets and CRCs | |
| World program ROMs (`b1.u27`/`b2.u26`), disassembled with Capstone | Checked every address above is used by the World code, and read the routines quoted in `map.yaml` | Not in the repo |

The RetroAchievements notes are community-written with no stated licence. We only take addresses (facts), credit
them, and don't copy achievement logic.

## World vs Japan

Every address from the `ddonpachj` sources is referenced in the World ROMs, often by the same code at the same
place: the rank routine is at `0x3362` in both. The code shifts in places (the trainer's CRC-skip patch is at
`0x5410` in Japan and `0x5404` in World), but the RAM layout looks the same. So `ddonpachj` can probably join this
pack once it's been checked.

## RetroAchievements' byte addresses are word-swapped

FBNeo keeps 68000 RAM as 16-bit words in the host's byte order, so on a little-endian machine each pair of bytes is
swapped. RA's 16-bit notes come out right (it reads them little-endian), but its **8-bit notes point at the other
byte of the word**: RA `0x2CB0` "bomb slots" is the 68000's `0x102CB1`, and RA `0x2CB1` "bombs" is `0x102CB0`. The
World code confirms this (bomb pickup at `0x4EC8A` increments `0x102CB0` up to `0x102CB1`), and so does the
trainer. To convert an RA 8-bit address: add `0x100000`, then flip bit 0.

FBNeo's `READ_CORE_RAM` returns the same swapped bytes (ship type B reads `01 00` at offset `0x2CA8`), so
`providers/retroarch.yaml` sets `word_swapped: true` and the provider swaps them back. Offset 0 is `0x100000`.

## First live check

One snapshot in play (type B, shot style, stage 1, two deaths, just respawned) decoded sensibly: `rank` was 21,
exactly what the formula gives (ship 1 + style 2 + normal stage 1 table 16 + 2 x 1 life), `p1.deaths_stage` 4
(2 deaths x 2), `p1.invincibility` counting down, `rank.survival_frames` 116.

Checked against the screen: score 2730 (`p1.score_digits` `00000273`, last digit 0), high score 5,908,065
(`00590806` and `hiscore.last_digit` 5 at `0x101744`), no spare lives shown with `p1.stock` 1, and 5 bombs.

## Rank

From the World code at `0x3362`, which epozzobon's formula matches, with some extra details:

- The players in play come from `0x5A688`: bit 7 set and bit 0 clear in `p1.flags` (`0x102C8C`) / `p2.flags`.
- Per player: `ship + 2 - 2 * style + 2 * shot_power + laser_power`, read with one `movem.w` from `0x102CA8`.
- The stage index is added for a player only while `pN.continues` is `0x100` (first credit), and again if
  `pN.deaths_stage` is 0.
- The sum is halved when both players are in.
- Then: `+ table[stage]` (pointer at `0x10122E`, set by difficulty), `+ 2 * (P1 lives + P2 lives)` (low bytes),
  `+ bees` (low byte of `0x103F8E`), `+ survival_frames >> 11`, `+ 32 * loop`, capped at 63.
- Afterwards: `0x10196B = rank / 2`, `0x10196D = rank / 4`, `0x10196F = max(rank - 32, 0) * 2`.

`derived.js` computes these parts (`rank.from_*`) and their total (`rank.model`). Over the whole of play-01 the
model matches `rank` except right after a change: the game only recalculates rank as the stage scrolls, so memory
catches up up to ~15 frames later. The rank gadget shows the game's value, with the model's reason for each change.

## Second loop

The check at `0x400B4` (World) matches the shmups wiki: P1 in play, `game.flags` bit 6 (solo), first credit
(`p1.continues` 0x100), then any of: `p1.deaths_first_credit` <= 2, `p1.max_hits` >= 270/300/330 for ship A/B/C
(BCD table at `0x40038`), `p1.score_digits` >= `0x05000000` (50,000,000), or `bees.perfect_stages` >= 4.
`p1.deaths_first_credit` stopped at 2 on the final death in play-01 (the game was over anyway).

## Bees and score per hit

13 bees per stage: the 13th makes the stage perfect (`0x4D2D2`). Bee values are in a table at `0x4D3A4`, indexed by
bees this stage + perfect stages so far, matching the wiki's 100 ... 100,000 and up to 1,000,000. Kills in a chain
score base value x hit count (+1,400 for the 14th hit on a 100-point enemy); the score only changes every other
frame, so `pN.hit_gain` credits a gain to a hit on that tick or the one before.

## Recording play-01

Type B shot, fresh credit on stage 1: a 14-hit chain, a bee, a power-up, three deaths with a bomb in between, and a
continue. `expected.yaml` checks it (`dotnet test src/server`). Findings, now in `map.yaml`:

- `p1.stock` drops at respawn, about 220 frames after the hit; `p1.flags` bit 0 marks the explosion, so
  `derived.js` takes the exploding ship off for the standard `p1.lives`.
- Rank matched the formula at every change. While a player's ship explodes, their own terms (ship, style, power)
  drop out, because `0x5A688` only counts players with bit 7 set and bit 0 clear.
- The chain gauge (`p1.hit_timer`) counts down a frame at a time, and hits top it up to 55. `p1.hits` keeps its
  value after the gauge runs out, until the next hit, so `derived.js` gives `p1.chain` as 0 once it's over.
- The last game's score stays in memory through the title and ship select, so `game.running` gates it.
- After a continue: `p1.continues` 0x101, bombs 6/6, power 4, score reset (its last digit now 1).

## Slowdown

`frame_counter` (`0x101270`) goes up once per pass of the main loop, which waits for vblank, so a pass that runs
long skips a frame (epozzobon's notes explain why). The board refreshes at 15625 / 271.5 = 57.55 Hz (MAME
`cave.cpp`), and play-01's ticks came at 57.59 a second, so each tick is one emulated frame. The counter advanced on
every tick on the ship select screen and early in stage 1, but on only 70-89% of them later in stage 1.
`game.speed` is that share, smoothed over about half a second. It assumes one read per emulated frame, which holds
for RetroArch at the board's own rate.

## Stage clear bonus

The results code (`0xF24`, `0x10CA`) pays 1,000 points per star counted at `0x103F90` and 500 per star at
`0x103F92` (both reset on death), plus the no-miss bonus if `pN.deaths_stage` is 0: 200,000-700,000 for 1-1 to 1-6
and 2,000,000-8,000,000 for 2-1 to 2-7 (tables at `0x14C8`). The wiki says loop 2 goes up to 7,000,000, but the
ROM's table has 8,000,000 for 2-7; unchecked in play.

## Distance to the boss

`stage.progress` is a signed scroll counter: -41 as stage 1 starts, then up 1 per scroll step. The boss positions
are RetroAchievements', not yet checked, and stage 7's isn't known. `boss.eta` divides what's left by the scroll rate
over the last ten seconds or so, so it's rough while the scroll speed changes.

## Future ideas

- **Hitbox overlay** (`0x102C9C`, 7x6 shrinking to 7x4 while moving sideways). On hold: drawing over the game feels
  too close to cheating, so the gadgets stay beside it for now.
- **Rank graph:** rank over the whole run, with markers at deaths and bombs, from `rank` and `p1.died`.
- **Personal-best splits:** compare `split.*` with a saved best run. Needs somewhere to keep it between sessions.

## Still to find or check

- Unverified variables, mostly P2's (a two-player session would check them) and `game.phase`.
- `game.state` on the results and ending screens, then whether `game.running` should exclude them.
- Max bonus, stage progress and the second loop need a longer run. The trainer's stage select would be quicker,
  but it patches `ddonpachj`, so this pack wouldn't match it: its recordings would only help once `ddonpachj`
  joins the pack.
- The object table (RA: 64-byte entries from `0x1001CE`), e.g. for boss health. Needs arrays in the map format.
- The boss positions in `stage.progress`, including stage 7's.
