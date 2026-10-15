# Memory maps

> Draft format. Nothing reads these files yet.

A memory map does one job: **match a running game to a set of named variables.**

```yaml
match:
  romsets: [invaders]
variables:
  hiscore: bcd2 @ 0x20F4
```

Gadgets use the variable names. Providers handle the addresses. The map is the only place where the two meet.

## Principles

- **Use the original hardware's addresses.** Write addresses the way a disassembly of the original game would (`0x20F8`), not as an offset inside an emulator's buffer. Providers translate them (see [providers.md](providers.md)).
- **Match exactly.** A map lists the ROM sets (MAME/FBNeo short names) it has been checked against. Different revisions often store values at different addresses.
- **Be explicit about encoding.** Every variable has a type, and the system declares its byte order: the 8080 is little-endian and the 68000 is big-endian.
- **Use standard names where they fit.** Using shared names (see below) means generic gadgets work with every game.
- **Mark what's verified.** Maps record whether each variable has been checked against a running game.

## Format

YAML, because maps are written by hand and need comments. One file per game: `maps/<id>.yaml`.

```yaml
schema: 1

game:
  id: invaders
  title: Space Invaders
  manufacturer: Taito
  year: 1978
  screen: { width: 256, height: 224, rotation: 270 }   # native raster; rotation as listed in MAME (ROT270)

match:
  romsets: [invaders]           # MAME / FBNeo short names

system:
  cpu: i8080
  endianness: little
  regions:
    work_ram:  { start: 0x2000, size: 0x0400 }
    video_ram: { start: 0x2400, size: 0x1C00 }

variables:
  # Short form: "<type> @ <address>"
  hiscore:          bcd2 @ 0x20F4
  p1.score:         bcd2 @ 0x20F8
  p2.score:         bcd2 @ 0x20FA
  credits:          bcd1 @ 0x20EB
  p1.lives:         u8   @ 0x21FF
  aliens_remaining: u8   @ 0x2082
  player.x:         u8   @ 0x201B

  # Long form, for when you need options
  game.running:
    type: bool
    address: 0x20EF
    description: 0 during attract mode / demo
    verified: false
```

> ⚠️ The Space Invaders addresses above come from memory of the
> [Computer Archeology](https://www.computerarcheology.com/Arcade/SpaceInvaders/)
> disassembly and **have not been verified**. Checking them against real memory dumps is the first task in the [roadmap](roadmap.md).

### Matching

`match` decides when a map applies. Providers report what's running (see [providers.md](providers.md#identifying-the-game)), and the core picks the first map that matches.

| Key | Use |
|---|---|
| `romsets` | Arcade games, by MAME/FBNeo short name: `[ddonpach, ddonpachj]` |
| `program_crcs` | Arcade games, by the CRC32 of each **program ROM chip** |
| `sha1` / `crc32` | Console games, by hashing the whole ROM file (planned) |

```yaml
match:
  romsets: [invaders]
  program_crcs:                 # the 8080 program ROMs (CRC values come from the spike dump)
    invaders.h: 0x00000000
    invaders.g: 0x00000000
    invaders.f: 0x00000000
    invaders.e: 0x00000000
```

#### Why chip CRCs, not file hashes, for arcade games

An arcade ROM set is a zip of many ROM chips. Hashing the zip itself doesn't work: the same set can be zipped with different file order, timestamps, compression or extra files, so two valid copies hash differently.

What stays the same is **the CRC32 of each chip**:

- The zip's file listing already stores them, so we can read them without unpacking anything.
- MAME and FBNeo list the same CRCs for each set in their ROM databases, so we know the expected values.
- The **program ROMs** hold the game's code, and the code decides where values live in memory. Matching on them means "this exact code", which is what a map's addresses depend on. Graphics or sound ROM differences (bootlegs with new graphics, for example) don't affect the map.

#### Precedence

1. If the provider reports chip CRCs, they decide: a map matches when all its `program_crcs` are present. The name is ignored, so renamed zips and naming differences between MAME and FBNeo don't matter.
2. Otherwise the core matches by `romsets` name.
3. If the name matches but the CRCs don't, the map is **not** used. A different revision under the same name would give wrong values silently, which is worse than showing nothing. Tooling reports the mismatch so the map can be extended.

If several ROM sets share most of their addresses but not all, a map can **extend** another and override only what's different (added once a game needs it):

```yaml
extends: ddonpach
match: { romsets: [ddonpacha] }
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

To make generic gadgets work with every game, maps should use these names whenever the game has the concept. Game-specific variables can use any name.

| Name | Meaning |
|---|---|
| `hiscore` | High score |
| `pN.score` | Player *N*'s score |
| `pN.lives` | Player *N*'s remaining lives |
| `pN.bombs` | Player *N*'s bomb stock |
| `credits` | Credits inserted |
| `game.running` | `true` during gameplay, `false` during attract mode or menus |
| `stage` | Current stage or loop |
| `rank` | Internal difficulty rank, as the raw value |
| `frame_counter` | Something that goes up by 1 each frame, used to detect missed frames (see [architecture](architecture.md#polling-isnt-frame-synced)) |

The list will grow as we add games.

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

1. **Record** a session with a marker at each event of interest ("bomb used", "life lost") using `stgadgets record`.
2. **RAM search** over the recording, the way cheat finders work: keep addresses whose value changed / didn't change / went up / went down / equals *N* between markers. Repeat until only a few candidates are left.
3. **Watch** the candidates live while playing to confirm them.
4. **MAME debugger** for the hard cases: set a watchpoint on an address to find the code that writes it, and work out what it means from there.

The RAM search and watch tools are milestone 3 in the [roadmap](roadmap.md).

## Tooling (planned)

- `stgadgets map check maps/invaders.yaml`: check the map against the schema
- `stgadgets map test maps/invaders.yaml tests/fixtures/recordings/invaders/...`: decode a recording and print the variables
- A JSON Schema for editor autocomplete
- Generating TypeScript types from a map, so gadgets get typed `useVar` names

## Open questions

- **YAML library:** YamlDotNet is the obvious choice. JSON with comments works with `System.Text.Json`, but it's noticeably worse to write by hand.
- **Per-player blocks:** `p1.*` / `p2.*` written out by hand, or a `players` construct that generates them?
