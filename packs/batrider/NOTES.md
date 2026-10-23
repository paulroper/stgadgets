# Armed Police Batrider research notes

How Armed Police Batrider works, as far as stgadgets has found out: where the map's addresses come from, the rules
derived values can follow, and what's still open. Addresses are the 68000's, and code addresses are the Japan B
version's (`batriderj`), unless noted.

## Sources

| Source | What it gave | Notes |
|---|---|---|
| [trap15's rank breakdown](https://shmups.system11.org/viewtopic.php?t=26814), posted by Icarus in the shmups forum's Batrider strategy thread | Rank variables (`0x20F9CE`, `0x20F9D0`, `0x20F9D4`, `0x20F9D8`), DIP and course bytes, the starting rank formula and table, and the code address and value of every rank change | `batriderj`. No licence stated; only facts are used |
| [Batrider MAME layout](https://www.speedrun.com/armed_police_batrider/resources/myd3g) by baf, Olifante and DUBS (v0.3) | Player blocks, ship and team, shot charge, medal value, item drop cycle and dropped-item counters, hidden boss conditions, stage order, enemy table and boss HP | `batriderj`. No licence stated; only addresses and rules are used |
| [sergiopolog's batrider-patches](https://github.com/sergiopolog/batrider-patches) (`patch.s`) | Rank, per-frame rank and minimum rank, credits, inputs, player active flags, the Stage Edit DIP copy | B version. GPL-3.0; only addresses are used here |
| [mountainmanjed's `batrider.lua`](https://github.com/mountainmanjed/MameLuaEngine/blob/master/batrider.lua) | Player, shot, enemy and enemy bullet tables and their strides, camera scroll | No licence stated; only addresses are used |
| [shmups wiki](https://shmups.wiki/library/Armed_Police_Batrider) | Rules: extends, special level-ups, hidden boss triggers, secret extends, codes | Rules only, to check against the code |
| [MAME `raizing_batrider.cpp`](https://github.com/mamedev/mame/blob/master/src/mame/toaplan/raizing_batrider.cpp) | Memory map (RAM `0x200000-0x20FFFF`), ROM sets and CRCs, screen (320×240, ROT270, about 59.6 Hz) | |
| [FBNeo `d_batrider.cpp`](https://github.com/finalburnneo/FBNeo/blob/master/src/burn/drv/toaplan/d_batrider.cpp) and libretro's `retro_memory.cpp` | How the FBNeo core lays out the RAM it exposes to RetroArch | |
| Japan B program ROMs (`prg0b.u22`, `prg1b.u23`, `prg2.u21`, `prg3.u24`), disassembled with Capstone | Checked the addresses above are used by the code, and read the routines quoted here | Not in the repo |

There are no RetroAchievements code notes: the game has no achievement set.

## Reading the game's memory

### ROM sets

Every Feb 1998 ("B") set shares `prg1b.u23`, `prg2.u21` and `prg3.u24`, and their `prg0` ROMs differ only in the
region byte at `0x000000` (Japan B `0x00`, Europe `0x02`, compared byte for byte). So `batrider` (Europe),
`batriderj` (Japan B), `batrideru`, `batriderc` and `batriderk` run the same code, and one map covers them all. The
older Dec 1997 sets (`batriderja`, `batriderhk`, `batridert`) have a different `prg1` and haven't been looked at.

The pack matches by ROM set name only. A split clone zip such as `batriderj.zip` holds just its own `prg0` (the rest
comes from `batrider.zip`), and a pack's `program_crcs` must all be in the zip, so no one list matches every B set.

| Set | `prg0` CRC | Shared program ROMs |
|---|---|---|
| `batrider` (Europe) | `prg0_europe.u22` `0x91d3e975` | `prg1b.u23` `0x8e70b492`, `prg2.u21` `0xbdaa5fbf`, `prg3.u24` `0x7aa9f941` |
| `batriderj` (Japan B) | `prg0b.u22` `0x4f3fc729` | the same |

### FBNeo's RAM layout

FBNeo exposes one RAM block for this driver, but not in the 68000's order, and with other memory in between:

| Core offset | 68000 address | Contents |
|---|---|---|
| `0x00000-0x01FFF` | `0x200000-0x201FFF` | text tilemap |
| `0x02000-0x06FFF` | `0x203000-0x207FFF` | work RAM: system, player blocks |
| `0x07000-0x0EFFF` | none | text tile data, mapped over `0x200000` only while the code draws text |
| `0x0F000-0x16FFF` | `0x208000-0x20FFFF` | work RAM: objects, game state, rank |
| `0x17000-` | `0x202000` | palette, then Z80 and GP9001 RAM |

The game's variables are in both work RAM areas, so no single `ram_base` covers them; the RetroArch core profile needs
a segment for each. Words come back byte-swapped, as for DoDonPachi (`word_swapped`). Checked live: the rank at core
offset `0x169D0` reads `0x00F7FA6C` once swapped, close to Normal's starting value.

### Line-A and line-F calls

The code uses `0xAxxx` and `0xFxxx` opcodes as compact calls into the line-A (`0x1F44`) and line-F (`0x345C`)
exception handlers. Capstone shows them as `dc.w`. `0xF0B8` follows each rank table lookup (death, extend), so it
probably applies `d0` to the rank; the handler hasn't been read.

## Game flow

There's no single game state byte. The main loop hands over between routines through `0x20F9C8` (written at `0x71C`,
`0x810`), but that's cleared again within a few frames. What works:

- **Stage** `0x20E6A9`: 1 for the first stage, 2 for the second, and so on, stepped by the stage advance at
  `0x10B92`. Bit 7 is set in the attract demo (`0x80`, set with `tas` at `0x108D6`), and it's 0 on the title and
  character select. It keeps its value through the continue screen and after game over.
- **Scene** `0x20E6AA`: the id the stage loader (`0xEE6A`) was given; it picks the stage's data. Manhattan (stage 1)
  is 1, and Sewage System, stage order value 3, was 4, so stages 2-4 are probably 1 + their stage order value. The
  attract and title screens use other ids (13, 23, 39, 40, 106).
- **In play:** the stage is 1-`0x7F` and a player's status (`+0x000`) has bit 7 set. Both players' status read 0 on
  the continue screen and after game over, and P1's kept bit 7 through a stage clear and results screen.

Continue screen and game over candidates, from one game: `0x20E6A2` read `0xFF` in play (and through a stage clear)
and 0 on the continue screen; `0x20E6A3` went to `0xFF` at game over (the attract loop tests it at `0x746`, and player
setup clears it at `0x2FAD4`); `0x20E6A5` stepped 0, 2, 4, 6 through the continue countdown.

`0x20E69E` is not the stage: it's the attract mode's demo cycle, stepped and wrapped at 25 by `0x75C`.

## Settings

| Address | Setting | Values | Source |
|---|---|---|---|
| `0x20F9FA` | Difficulty DIP | 0 easy, 1 normal, 2 hard, 3 very hard | trap15 (read 1 at defaults) |
| `0x20F9FB` | Timer rank DIP | 0 low, 1 normal, 2 high, 3 highest | trap15 (read 1 at defaults) |
| `0x20FA02` | Stage Edit DIP (DSW3 switch 2) | `0xFF` on | patch |
| `0x20E6A6` | Course | 0 training, 2 normal, 4 advanced, 6 boss | trap15; read 2 on Normal. The attract demo also writes it (`0x790`) |
| `0x20F9FD` | Team size − 1 | 2 for a three-ship team | Code (loops at `0x2FA7C`, `0x30152`); read 2 |

## Credits

`0x203400` (byte) is the credits not yet used: it went 0 → 1 with a coin. Its long word also flags free play (patch).
Unused credits add to the rank every frame (see Rank).

## Player blocks

P1's block starts at `0x204062` and P2's at `0x2041D4`, `0x172` bytes apart. Offsets from the block's start:

| Offset | Size | Meaning | Checked |
|---|---|---|---|
| `+0x000` | u8 | Status: bit 7 active. `0x8E`/`0x8F` in play, `0x81` while dying, bits 4-5 set while a bomb goes off, 0 after game over | Recording |
| `+0x012` | u16 | Current ship; ship id `(v >> 4) & 31`, type `(v >> 2) & 3` | Recording: changed at each respawn |
| `+0x01A` | u16 × team size | Team list; bit 0 set means that pilot is down (see Lives) | Recording, code |
| `+0x04A`, `+0x04C` | s16 | Position (y, x), in 1/128 pixel | Lua |
| `+0x0F4` | u32 | Score, binary | Recording: matched the screen exactly |
| `+0x112` | u8 | Full bombs | Recording; cleared on respawn (`0x2FADA`) |
| `+0x113` | u8 | Bomb fragments | Recording: matched the screen; cleared on respawn |
| `+0x117` | u8 | Shot charge, full at `0x48` | Recording, overlay |

So P1's score is at `0x204156` and P2's at `0x2042C8`.

## Frame counter, high score and continues

- `0x20FD44` (u32) is added to by the vblank interrupt (`0x1C52`), so it counts every frame, slowdown included. It
  was reset with the board, and stood still only while RetroArch was paused.
- `0x20FAE8` (u32) is the high score: it read the 1,613,210 shown throughout play. Briefly 500,000 at a reset before
  the saved table came back.
- P1 + `0x116` counts continues used, up to 9 (player setup at `0x2FAF0`). It went 0 to 1 on the continue screen. The
  score cap (`0x7EC2`) is 63,999,990 plus this count, so the score's last digit probably counts continues as in
  DoDonPachi; not yet seen after a continue.

## Lives and extends

There's no lives counter. Each player has a team list at `+0x1A`, one word per pilot (`0x20F9FD` + 1 of them), and a
pilot is down when bit 0 of their word is set. Lives are the pilots not down. In a recorded game with a three-ship
team, the first pilot's bit was set at the first death, the second's at the second, and the third death ended the game.

- **Death** (`0x2FA6E`) counts the pilots left, to pick the rank change (table at `0x2FA9E`). If `0x20E69D` is
  non-zero it uses 0 instead.
- **Extend** (`0x3014E`): if the first pilot in the list is up (nobody is down), the extend pays 100,000 points instead
  (`0x3013A`). Otherwise it clears the first pilot's bit and rotates them to the end of the list. So lives never go
  above the team size.
- **Secret extend:** the extend code has its own path (`0x30194`) with a team of 3 or more, fewer than 2 pilots left,
  and the dropped-1UP counter (`0x20E460`) at 3. The shmups wiki's "miss 3 extend items" rule.
- **Score extends** (`0x7E6E`): when the score reaches P1 + `0xF8` (`p1.next_extend`), the next threshold is
  `settings.extend_last` (`0x20FA0E`) if that's set, and after it none (`0xFFFFFFFF`); otherwise it's
  `settings.extend_every` (`0x20FA0A`) higher. At the defaults the first is 1,500,000 and they come every 1,500,000,
  as the shmups wiki says. Not yet seen reached.

Respawning loads the next pilot's own bombs and fragments, so they can go up at a death: one game went from 1 bomb and
26 fragments to 3 bombs and 6.

## Bombs

Full bombs are at `+0x112` and fragments at `+0x113`. Fragments count up as pieces are collected; at 40 they wrap to
0 and add a full bomb (they read 37, 39, then 0 with one more bomb). The overlay's dropped-bomb counter turns red at 5.

## Shot and options

Each pilot has their own power, in the player block, and all of it was 0 again when the next pilot came in:

| Offset | Meaning | Seen |
|---|---|---|
| `+0x110` | Shot level, 0-3 | Up by one at each shot level-up the screen showed, small or large |
| `+0x111` | Small shot items towards the next level | Counted up by small shots, cleared by every level-up |
| `+0x118` | Options | 2 after the first option item, with two options on screen |
| `+0x104` | Option level | Up by one at each option item after the first; not yet seen at a respawn |
| `+0x022` | Option formation (word) | Set by the special options (below); not yet seen |

The rules, from the pickup routines:

- **Small shot** (`0x2FCA2`): at level 3 it scores 100 points. If three small shots were let fall in a row
  (`dropped.small_shot` = 3), it's a special level-up (below). Otherwise it counts towards the next level, which
  takes 2, 3 and 4 items at levels 0, 1 and 2 (table at `0x2FCDE`). In play, level 0 to 1 took 2 and 1 to 2 took 3; a
  small shot at level 3 left the count alone. The shmups wiki's 1, 2, 4, 8 doesn't match.
- **Large shot** (`0x2FD94`): at level 3 it scores 5,000. After three dropped it's a special level-up; otherwise it
  adds a level and clears the small item count. Three large shots in play took the level 0 to 1, 1 to 2 and 2 to 3,
  the last clearing a count of 1.
- **Special shot level-up** (`0x2FDE6`): level 3 at once, `special.shot` set, and 1 more rank a frame.
- **Bomb fragments** (`0x2FD24`): 40 make a bomb. With 4 bombs they stop at 40, and a fragment then scores 1,000.
- **Large bomb** (`0x2FE12`): a bomb; a fifth makes it 4 bombs and 40 fragments, and when full it scores 10,000.
- **Option** (`0x2FE72`): each ship has a table (`0x2FF1A`, by ship id) of how many of each item must have been
  dropped (options, medals, bomb fragments, small shots, large shots); the first that matches sets that formation in
  `+0x022` (`0x0A`, `0x0C`, `0x0E`, `0x10`, …). These are trap15's shadow, wide, rolling, search and homing options.

## Items, medals and drops

- **Medal value** `0x20E459` is written by the medal routine (`0x30010`): twice the next medal's place in the scale.
  It went 0, 2, 4, 6 as 100, 200 and 300 point medals were taken in a row (the score went up by each), and back to 0
  when one dropped. Per the shmups wiki, medals go 100 to 900 in hundreds, then 1,000 to 10,000 in thousands, and
  dropping one resets the chain; `medal.next` in `derived.js` follows that scale, checked up to 600.
- **Drop cycle** `0x20E457`: the position in a fixed cycle of 25 item drops, stepped and wrapped at `0x39DA6`. The
  overlay shows the next six from it.
- **Dropped items**, one byte each, counting items of that kind let fall since the last one was taken (cleared on
  pickup, e.g. `0x2FCB8`):

  | Address | Item | Overlay highlights at |
  |---|---|---|
  | `0x20E45A` | small shot | 3 |
  | `0x20E45B` | bomb fragment | 5 |
  | `0x20E45C` | medal | |
  | `0x20E45D` | large shot | 3 |
  | `0x20E45E` | large bomb | 3 |
  | `0x20E45F` | option | 3 |
  | `0x20E460` | 1UP | 3 |

  The thresholds are the special level-ups and secret extends: drop 3 of a kind and the next one is special.

## Rank

From trap15's breakdown, with its code addresses found in the Japan B ROM:

- `0x20F9D0` (u32) is the rank, stored inverted: lower is harder. It's set to the minimum `0x20F9D8` at character
  select, and is never above it or below 0.
- `0x20F9D8` = `rank_start[difficulty] × 0x20F9CE × course_mul[course]`. `0x20F9CE` is `0x100` if Start was used to
  leave test mode or held at boot, else `0xC0`, until the board is reset. Normal starts at `0xF80000` (read
  `0xF80000`), Advanced `0xD90000`, Boss `0xC98000`.
- Each frame the rank drops by `0x20F9D4` plus the unused credits. `0x20F9D4` starts from the Timer rank DIP (0, 4,
  `0x10`, `0x40`; read 4 at defaults) and resets on a continue.
- **Death:** eases it by `0x400000`, `0x100000`, `0x40000` or `0x10000` with 0, 1, 2 or 3 pilots left.
- **Extend:** raises it by `0x80000`, `0x100000`, `0x200000` or `0x400000` with 1, 2, 3 or 4 pilots up before it.
- **Items:** small shot `0x100`, cash `0x400`, bomb fragment `0x800`, large shot and option `0x8000`, large bomb
  `0x20000`. Medals: `0x100` for 1,000-9,000, `0x200` for 10,000. Special items add 1 or 2 to the per-frame rank.
- **Shots:** each main shot bullet adds 6 (piercing `0x28`); option bullets add a per-character amount.
- **Bombs, graze, seals:** a full bomb adds 0 and a partial bomb `0x8000`; each grazing bullet eases it by `0x80` a
  frame; each sealed bullet adds `0x800`.

Shown as a percentage, the overlay uses `1 − rank / 0x1000000`, and the patch `(min − rank) / 0x687` (0.01% steps).

## Stage scripts and boss positions

Each scene's stage script drives the stage: the stage loader (`0xEE6A`) takes the scene's record from the table at
`0xEF94` (by `stage.scene`), and the record's long at offset 22 is the main script, run by `0x10750` against
`stage.scroll` (`0x2034DA`). A script is a list of commands, each a word (class byte, op byte) and its arguments:

- **Classes 0-7 spawn enemies** through the handler tables at `0x107AA`; class 2 is the bosses (each handler sits just
  before its boss's routine: op `0x00` CONFRICT, `0x14` SOBUT, ...). Their arguments vary by enemy: 8 bytes of position
  for most (`0x3A092`), more for some (`0x39F92` reads 16).
- **Class 8 is control** (table at `0x107D2`). The ones that matter here: `0x0830` waits until the scroll reaches a
  position (absolute, or relative to now with bit 31 set) and stops the script there; `0x0814` jumps, `0x0818` and
  `0x081C` call and return, `0x0828` branches by course, `0x0878` calls by the stage order, `0x0810` sets the scroll's
  target speed and acceleration.

Stage 1's script (`0x220F0`) waits for `0x6F000` (`0x221B0`), then after three setup commands spawns CONFRICT. The
boss appeared at a scroll of 454,818 in four games, two frames' scroll past it. `stage.scroll_speed` (`0x2034C8`, 128
a frame when cruising) gives the time left.

Walking forward from each wait over control commands to a class 2 spawn finds these, not yet checked in play (scene
ids are `stage.scene`):

| Scene | Boss spawns after a wait for |
|---|---|
| 3 | BOREDOM `0x168000` |
| 4 (Sewage System) | DEVIATE `0x5B000`, and `0x3000` |
| 6 | GRUBBY `0x9DC00`, and `0x8000` |
| 7 | DISCHARGE `0xD3800` and `0x15800`; an unnamed boss (handler `0x64F20`) `0xE4000` and `0x2A000` |
| 8 | BASHINET MK2 `0x8400` |
| 11 | GLOW SQUID `0x40800` |

Scenes 2 and 5 have none this way: their bosses must come from a sub-script or after a relative wait. Two waits in a
scene may be two scroll sections or two courses' branches. Envy, Blunt and Sobut are the Highway's bosses (picked by
the stage order), not stages 2-4's. To add a stage to the boss gadget, check its position in a recording (the boss
arrives just past it) and add it to `BOSS_AT` in `derived.js`.

## Courses and routes

There's no stage count in RAM: the route is in the master script (scene 0's, `0x21BD8`), which branches by course
(`0x21C20`) and calls each stage's script in turn. `0x0878` calls the stage in a slot of the stage order through the
table at `0x21EE8`, so order value *v* is scene *v* + 1. Stage names are the game's, from the table it draws them with
(`0x11C0C`, by scene): 1 M.CITY, 2 AIRPORT, 3 SKY HIGH, 4 S.SYSTEM, 5 HIGH-WAY, 6 Z.CITY, 7 BUILDING, and the hidden
bosses' 8 BASHImk2, 9 BASHI-R, 10 B.H.mk2, 11 G.SQUID.

| Course | Script | Stages |
|---|---|---|
| Training (0) | `0x21C9A` | M.CITY, the first chosen stage, HIGH-WAY |
| Normal (2) | `0x21CB4` | M.CITY, the three chosen, HIGH-WAY |
| Advanced (4) | `0x21CEA` | M.CITY, the three chosen, HIGH-WAY, Z.CITY, BUILDING |
| Boss (6) | `0x21D50` | A boss rush, from its own scripts; not decoded |

Hidden bosses' stages are conditional calls in the course scripts (class 5 commands `0x90`, `0x94`, `0x9C`, `0xA0`,
whose handlers sit beside those bosses' code, skip them unless their condition is met): BASHImk2 before the HIGH-WAY,
B.H.mk2 after it, and BASHI-R and G.SQUID after Z.CITY. Each adds to `stage.counter`, which is why the route goes by
scene. Seen in play: the normal course's M.CITY then S.SYSTEM (order value 3) as stage 2.

## Stage order and hidden bosses

- `0x20F9E0`-`0x20F9E2`: the order of stages 2-4, read by the stage loader (`0x10BEC`), set before character select.
  Two games read 3, 2, 1 and 3, 1, 2; the second's stage 2 was Sewage System, so 3 is Sewage System. The overlay's
  conditions pair 1 with Envy, 2 with Blunt and 3 with Sobut, and the shmups wiki puts Sobut in the Sewer System.
- `0x20E463`: special shot level-up taken (set at `0x2FDF8`); `0x20E464`: special option level-up (overlay; not
  found in the code yet).
- `0x20E6B2`-`0x20E6B7`: counters for the hidden boss triggers. The overlay's conditions: Glow Squid `0x20E6B2` = 4,
  Bashinet-R `0x20E6B3` = 3, Bashinet Mk2 `0x20E6B4` = 2, Gob-Robo `0x20E6B5` = 2, Bashinet `0x20E6B6` = 0, Black
  Heart `0x20E6B7` = 5. Each also needs a pilot of the right origin on the team; Envy, Blunt and Sobut depend on the
  stage order and the special level-ups, and Hayate, Tsumujimaru and Black Heart Mk2 on the team's mix of powers,
  shots and ship types. The overlay has the ship tables.

## Enemies and bosses

The enemy table is at `0x20869A` (`enemies` in the map): 96 slots, `0x72` bytes apart. Per slot: status word (bit 15 active), routine
address (u32) at `+2`, part id at `+0x20`, HP (u16) at `+0x50`. The overlay identifies bosses by routine address and
reads their HP and timers.

Checked for stage 1's boss, CONFRICT (routine `0x0531B4`): it used about 28 slots, one per part. Its body (part 0)
went from 84 HP to 0 between the boss appearing and being destroyed, and other parts (e.g. part 118, 42 HP) wore
down separately. The overlay picks which part's HP to show with a status mask per boss and part (for CONFRICT, part
118 while status `& 0xF001` is `0x8000`, then part 0 while status `& 0xF201` is `0x8200`).

## Still to find or check

- The scene ids of the other stages, and the stage order values' stages (3 is Sewage System).
- Boss HP for the other bosses, and the boss timers.
- The hidden boss counters, in play.
- Score extends (1.5 million) and the cash item, in the code.
- P2's block, in a two-player game.
- The Dec 1997 sets.

## The layout

`ui/layout.tsx`. Left: score (with the points just scored), high score, lives as pilots with the next extend, bombs
and fragments, shot and option power, stage; then rank and the next medal. Right: the boss's HP and the route, the
items let fall towards a special, and latency.

- **Rank** is shown as the MAME layout does, `1 - rank / 0x1000000` as a percentage. Its breakdown (`rank.from_*` in
  `derived.js`) puts each tick's change down to time (the timer rank and unused credits, per frame of
  `frame_counter`), deaths and extends (trap15's tables, by pilots left or up), pickups it can see (shot and option
  levels, small shot items, fragments, large bombs, medals), and the rest, which is shots and grazing. When an event
  would take rank past `rank.min` or 0, it's counted only as far as rank went. Not seen, so in the rest: small shots
  at the top level, cash, bombs used, bullet seals. Over the recorded games the parts added up to the rank's change
  since play started.
- **Boss HP** uses the MAME layout's table of boss routines and parts; checked on CONFRICT only. Before the boss
  arrives, the gadget shows the scroll's progress towards it and the time left, on stages in `BOSS_AT` (stage 1 so far).
- **Route**: the course's stages from the ROM (below), found by `stage.scene`, with a hidden boss's stage as an extra
  line. Names are the game's own.

## Future ideas

- Lives as the team's portraits: who's up, who's next.
- The item drop cycle (`drop.position`): what's coming.
- A hidden boss checklist for the current team and stage order.
- Boss time-out timers: how long until a boss leaves, from the MAME layout's per-boss timer fields (enemy table
  offsets 66, 70, 72, 74, 96 and 104, and `0x20E9B8`), as a second line in the boss gadget.
- The hidden option formations: what each ship needs dropped for each one.
