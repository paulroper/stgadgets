# Memory maps

> Early format. `Stgadgets.Core` loads it (`MemoryMapLoader`), but expect changes as more games arrive.

A memory map does one job: **give a game's memory named variables.** Each [game pack](packs.md) has one, `map.yaml`, and the pack's `pack.yaml` says which ROM sets it applies to.

```yaml
variables:
  hiscore: bcd2 @ 0x20F4
```

Gadgets use the variable names. Providers handle the addresses. The map is the only place where the two meet.

## Principles

- **Use the original hardware's addresses.** Write addresses the way a disassembly of the original game would (`0x20F8`), not as an offset inside an emulator's buffer. Providers translate them (see [providers.md](providers.md)).
- **Match exactly.** A pack lists the ROM sets (MAME/FBNeo short names) its map has been checked against. Different revisions often store values at different addresses.
- **Be explicit about encoding.** Every variable has a type, and the system declares its byte order: the 8080 is little-endian and the 68000 is big-endian.
- **Use standard names where they fit.** Using shared names (see below) means generic gadgets work with every game.
- **Mark what's verified.** Maps record whether each variable has been checked against a running game.

## Format

YAML, because maps are written by hand and need comments. One file per game: `packs/<id>/map.yaml`.

An excerpt from the first real map, [`packs/invaders/map.yaml`](../packs/invaders/map.yaml), checked against recordings:

```yaml
schema: 1

system:
  cpu: i8080
  endianness: little
  regions:
    work_ram:  { start: 0x2000, size: 0x0400 }
    video_ram: { start: 0x2400, size: 0x1C00 }

# Addresses from the Computer Archeology disassembly
# (https://www.computerarcheology.com/Arcade/SpaceInvaders/), verified against
# recorded play (RetroArch 1.22.2, FBNeo).
variables:
  hiscore:          bcd2 @ 0x20F4   # updated at game over
  p1.score:         bcd2 @ 0x20F8
  p2.score:         bcd2 @ 0x20FC   # each score is followed by 2 bytes of screen position
  credits:          bcd1 @ 0x20EB
  aliens_remaining: u8   @ 0x2082   # 55 at the start of a wave; the mystery ship doesn't count

  p1.ships_reserve:
    type: u8
    address: 0x21FF
    description: Spare ships, not counting the one in play. Doesn't change on the final death.
    verified: true

  player.state:
    type: u8
    address: 0x2015
    description: 0xFF while alive; alternates 0/1 for the explosion animation (~0.85 s) after a hit.
    verified: true
```

Variables use either the **short form** `"<type> @ <address>"` or the **long form**, a mapping with `type`, `address` and options.

### Matching

`match`, in the pack's [`pack.yaml`](packs.md#packyaml), decides when a pack and its map apply. Providers report what's running (see [providers.md](providers.md#identifying-the-game)), and the server picks the first pack that matches.

| Key | Use |
|---|---|
| `romsets` | Arcade games, by MAME/FBNeo short name: `[ddonpach, ddonpachj]` |
| `program_crcs` | Arcade games, by the CRC32 of each **program ROM chip** |
| `sha1` / `crc32` | Console games, by hashing the whole ROM file (planned) |

```yaml
# pack.yaml
match:
  romsets: [invaders]
  program_crcs:                 # the 8080 program ROMs
    invaders.h: 0x734f5ad8
    invaders.g: 0x6bfaca4a
    invaders.f: 0x0ccead96
    invaders.e: 0x14e538b0
```

#### Why chip CRCs, not file hashes, for arcade games

An arcade ROM set is a zip of many ROM chips. Hashing the zip itself doesn't work: the same set can be zipped with different file order, timestamps, compression or extra files, so two valid copies hash differently.

What stays the same is **the CRC32 of each chip**:

- The zip's file listing already stores them, so we can read them without unpacking anything.
- MAME and FBNeo list the same CRCs for each set in their ROM databases, so we know the expected values.
- The **program ROMs** hold the game's code, and the code decides where values live in memory. Matching on them means "this exact code", which is what a map's addresses depend on. Graphics or sound ROM differences (bootlegs with new graphics, for example) don't affect the map.

#### Precedence

1. If the provider reports chip CRCs, they decide: a pack matches when all its `program_crcs` are present. The name is ignored, so renamed zips and naming differences between MAME and FBNeo don't matter.
2. Otherwise the core matches by `romsets` name.
3. If the name matches but the CRCs don't, the pack is **not** used. A different revision under the same name would give wrong values silently, which is worse than showing nothing. Tooling reports the mismatch so the map can be extended.

**Merged sets:** a MAME *merged* zip (like the `invaders.zip` used in the spike) contains the parent's ROMs at the top level and every clone's ROMs in subfolders. Only the top-level files describe the parent. Clone CRCs being in the zip doesn't mean those clones are running.

Currently only name matching is implemented. CRC matching comes when a provider reports chip CRCs.

Clones and revisions belong to their parent's pack. If one shares most of its addresses with the parent but not all, its map will be able to **extend** the parent's and override only what's different (added once a game needs it; the exact shape is still open):

```yaml
extends: ddonpach
romsets: [ddonpacha]
variables:
  rank: u16 @ 0x...           # the only address that differs
```

### Types

| Type | Meaning |
|---|---|
| `u8` `u16` `u32` | Unsigned integer, in the system's byte order |
| `s8` `s16` `s32` | Signed integer |
| `u16le` `u16be` … | Byte order given explicitly, overriding the system's |
| `bool` | Zero is false, anything else is true |
| `bcdN` | Binary-coded decimal across *N* bytes (2 digits per byte), common for scores. Bytes are read in the system's byte order |
| `bytes[N]` | A raw block of *N* bytes, sent to the layout as an array |

Long-form options. Following the [roadmap principle](roadmap.md#principles), anything beyond what Space Invaders needs gets built only when a real game needs it:

| Option | Meaning |
|---|---|
| `mask`, `shift` | Pick out one bit or a group of bits: `{ type: bool, address: 0x2015, mask: 0x80 }` |
| `scale` | Multiply after decoding, e.g. `10` for games that store the score without its last digit |
| `description` | Free text, shown in tooling |
| `verified` | `true` once checked against a running game |

Expected later, once a game needs them:

- **Arrays and structs:** enemy tables, bullet lists, per-player blocks with an address step between them
- **Pointers:** an address that comes from reading another variable, common on 68000 games such as DoDonPachi

### Standard variable names

To make generic gadgets work with every game, packs should provide these names whenever the game has the concept. Game-specific variables can use any name.

A standard name can come from `map.yaml` or from [`derived.js`](#derived-values), and gadgets can't tell the difference. Games store things in their own ways, and `derived.js` is where a pack adapts them to the standard meaning. For example, Space Invaders stores spare ships, and computes `p1.lives` from them. If the game's own byte doesn't mean exactly what the standard name means, give the raw byte a game-specific name and derive the standard one. Invaders' `gameMode` is `game.mode`, because it's also true on the start screen.

| Name | Meaning |
|---|---|
| `game.running` | `true` while a game is being played, `false` in attract mode (including demo play), menus, and screens that hold play still, such as "get ready" and game over. **This is the gate.** Outside play, memory holds leftovers (Invaders' alien count reads 255 on its start screen), so derived values and gadgets should treat other variables as meaningless while this is `false`, and derived values should return `null` |
| `hiscore` | High score |
| `pN.score` | Player *N*'s score |
| `pN.lives` | Player *N*'s lives, **including** the one in play. A ship that's exploding no longer counts. `null` when it isn't known, such as outside play |
| `pN.ships_reserve` | Spare lives, **not** counting the one in play. Many games store this instead of `pN.lives` (Space Invaders does) |
| `pN.alive` | `true` while player *N*'s ship is in play, `false` while it's exploding or respawning. `null` outside play, or while another player is up |
| `pN.bombs` | Player *N*'s bomb stock |
| `credits` | Credits inserted |
| `stage` | Current stage or loop |
| `rank` | Internal difficulty rank, as the raw value |
| `frame_counter` | Something that goes up by 1 each frame, used to detect missed frames (see [architecture](architecture.md#polling-isnt-frame-synced)) |

The list will grow as we add games.

## Derived values

Some values follow from memory plus knowledge of the game: a table in ROM, a rule from the disassembly, or what happened on earlier frames. The map stays raw data, and these values go in the pack's `derived.js`. The server computes them each tick, straight after decoding, and publishes them like any other variable. Layouts just display them, and recordings check them in `expected.yaml` ([ADR 0012](decisions/0012-derived-values-in-javascript.md)).

`derived.js` default-exports an object. Each entry is a function `(v, prev) => value`, or `{ description, value }` when it's worth explaining (the description is shown in tooling, like a map variable's):

```js
// based on packs/invaders/derived.js
const ALIENS = [50, 43, 36, 28, 22, 17, 13, 10, 8, 7, 6, 5, 4, 3, 2, 1];   // ROM table at 0x1A11

export default {
  'fleet.speedup_in': {
    description: 'Kills until the march speeds up again',
    value: v => {
      const i = ALIENS.findIndex(t => t <= v.aliens_remaining);
      return i < 0 || i === ALIENS.length - 1 ? null : v.aliens_remaining - ALIENS[i] + 1;
    },
  },
  // State across ticks, via the previous tick's values.
  frames_since_kill: (v, prev) =>
    prev === null || v.aliens_remaining < prev.aliens_remaining ? 0 : prev.frames_since_kill + 1,
};
```

- **`v`** holds this tick's values: the map's variables, plus derived values declared earlier in the object. Names with dots need brackets: `v['p1.score']`.
- **`prev`** holds the previous tick's values, derived ones included, or `null` on the first tick (including after the game restarts).
- **Return** a number, boolean, string or `null` (no value). Whole numbers are sent as integers.
- **A function that fails** gives `null` for that tick. Failing means it throws, returns `undefined` (usually a misspelt name), returns `NaN` or `Infinity`, or returns anything else. The server logs it once and recordings report it.
- **Keep functions pure and quick.** Scripts run in a sandbox: strict mode, no I/O, no .NET, and at most one frame of time per tick. Loading the script, once at game start, may take longer (up to 2 s), since the first script in the process is slow to start. A typical tick takes microseconds.
- **Names** can't repeat a variable from `map.yaml`. Use the [standard names](#standard-variable-names) when a derived value is one of those concepts, such as `pN.lives` computed from `pN.ships_reserve`.
- **Gate on `game.running`.** Declare it first, and return `null` from other values while it's `false`, because memory outside play holds leftovers.
- **Number traps:** values are JavaScript numbers. They're exact for anything a map decodes, but bitwise operators work on signed 32 bits, so pick out bits with `mask`/`shift` in the map instead.

## Finding addresses

Writing maps is the real bottleneck. Space Invaders has a published disassembly. Most games don't, and things like DoDonPachi's rank, chain timer and hyper meter will need detective work.

### Existing sources (check these first)

| Source | What it gives | Caveats |
|---|---|---|
| **MAME cheat files** (e.g. Pugsy's cheat collection) | The original hardware's addresses for lives, bombs, invincibility, stage and so on, for most arcade shmups | Cheats *write* memory, but the addresses are what we want. Each cheat is tied to a MAME ROM set |
| **RetroAchievements code notes** | Annotated addresses for each game, often very detailed | Addresses are in the core's layout, not the original hardware's, so they need translating per core |
| **Disassemblies and community research** | Full explanations of how the game works (rank formulas and so on) | Only exist for well-studied games |

Credit the source in the map's comments.

### Finding them ourselves

1. **Record** a session with a marker at each event of interest ("bomb used", "life lost").
2. **RAM search** over the recording, the way cheat finders work: keep addresses whose value changed / didn't change / went up / went down / equals *N* between markers. Repeat until only a few candidates are left.
3. **Watch** the candidates live while playing to confirm them.
4. **MAME debugger** for the hard cases: set a watchpoint on an address to find the code that writes it, and work out what it means from there.

Steps 1–3 are packaged as the `game-recording` Claude skill (`.claude/skills/game-recording/`): `record.py` records with live markers and watched addresses, and `recording.py search` / `history` do the filtering and confirming. Recordings are local and git-ignored ([ADR 0011](decisions/0011-recordings-are-local.md)).

## Tooling (planned)

- `stgadgets pack check packs/invaders`: check the pack against the schema
- `stgadgets pack test packs/invaders`: replay the pack's recordings and check their `expected.yaml` (the same harness `dotnet test src/server` runs today; see [packs.md](packs.md#testing))
- A JSON Schema for editor autocomplete
- Generating TypeScript types from a map, so gadgets get typed `useVar` names
- Reporting standard-name coverage, such as "provides 7 of 12 standard names", so gaps in the generic layout are visible. Standard names stay optional

## Open questions

- **YAML library:** YamlDotNet is the obvious choice. JSON with comments works with `System.Text.Json`, but it's noticeably worse to write by hand.
- **Per-player blocks:** `p1.*` / `p2.*` written out by hand, or a `players` construct that generates them?
