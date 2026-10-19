# The stgadgets CLI

`stgadgets` is a command-line tool for developing game packs. It lives in `src/cli/` and uses the same
`Stgadgets.Core` library as the server (`src/lib/`), so it decodes memory and runs `derived.js` exactly as the
server does, without the server or an emulator.

Run it from the repository with `dotnet run` (below, `stgadgets` is short for that):

```sh
dotnet run --project src/cli/Stgadgets.Cli -- <command>
dotnet run --project src/cli/Stgadgets.Cli -- --help
```

`dotnet test src/cli` builds and tests it.

## Recordings

Recordings are local and git-ignored ([ADR 0011](decisions/0011-recordings-are-local.md)); the `game-recording`
Claude skill makes them and explains the workflow. These commands read one from its folder. Inside a pack
(`packs/<id>/recordings/<name>`) they use the pack's map and `derived.js` too.

A **point** in a recording is a tick number, `start`, `end`, or a marker label: exact, or a prefix that only one
label starts with.

### `stgadgets recording markers <recording>`

How many ticks the recording has, and the tick of each marker:

```
3741 ticks
     787  coin inserted
    1635  chain reaches 14
    3518  last life lost, continue screen
```

### `stgadgets recording search <recording> <filters>…`

Narrows down which addresses hold a value, the way cheat finders do. Each filter compares one byte between two
points, or with a value, and keeps the addresses that pass. Filters apply in the order given:

| Filter | Keeps addresses whose byte… |
|---|---|
| `--changed <from> <to>` | changed between the points |
| `--unchanged <from> <to>` | didn't change |
| `--increased <from> <to>` | went up |
| `--decreased <from> <to>` | went down |
| `--equals <point> <value>` | equals the value (decimal or `0x` hex) at the point |

```sh
stgadgets recording search REC --decreased "player hit (1st" "player hit (2nd" --equals end 0
```

```
after --decreased player hit (1st player hit (2nd: 35 candidate(s)
after --equals end 0: 12 candidate(s)

address          start  mystery ship  player hit (  spare ships   player hit (  player hit (  game over, h           end
0x002005             0             0             1             1             0             0             0             0
...
```

The table shows each candidate at the start, every marker and the end. `--show` sets how many are listed (20), and
`--region` searches one region of the recording.

### `stgadgets recording history <recording> <address-or-name>`

Every distinct value over the recording, with the ticks it changed at and the markers there. Give an address to
read it with `--type`, as in `map.yaml` (`u8` by default, `u16`, `s16be`, `bcd2`, …), in the pack map's byte order
(`--endianness` for a recording not in a pack yet). Or give the name of a map variable or derived value, which is
read through the pack:

```sh
stgadgets recording history REC 0x20F8 --type bcd2
stgadgets recording history REC p1.lives
```

```
tick      0         3
tick    587         2   <- player hit (1st death)
tick    854         1   <- player hit (2nd death)
```

### `stgadgets recording values <recording> --at <point> [--only <names>]`

Every value the server would publish at points in the recording, map and derived alike, as JSON. Repeat `--at` for
several points. `--only` keeps the names that match, where `*` matches anything (`--only 'rank*' --only p1.score`).

```sh
stgadgets recording values packs/ddonpach/recordings/play-01 --at "chain reaches 14" --only 'rank*'
```

```json
{
  "chain reaches 14": {
    "tick": 1635,
    "values": {
      "rank": 25,
      "rank.change_reason": "ship & power -9",
      "rank.model": 25
    }
  }
}
```

The recording is replayed from its first tick, so derived values that build up over time (splits, ages, changes)
are what they'd be at that point in play. Derived values that fail are reported on stderr.

Use it to start a pack's [game states](packs.md#testing-the-ui) and [scenarios](packs.md#testing-derived-values)
from real play. The values are the recording's, but committed files describe the moment, not the recording: they
never name it or its ticks.

### `stgadgets recording mark <recording> <tick> <label>`

Adds a marker at a tick, or moves the marker with that label there. Use it once `history` shows exactly when
something happened.

### `stgadgets recording trim <recording> <first> <last> --out <folder>`

Copies the ticks from one point to another, with their markers, to a new recording, so a long session can shrink to
the part worth keeping. Timestamps start again from 0, and the manifest's notes say where it came from.

## Planned

- `stgadgets pack check <dir>`: check a pack against the schema
- `stgadgets pack test <dir>`: run a pack's scenarios and recording checks, for a pack anywhere on disk
