# The stgadgets CLI

`stgadgets` is a command-line tool for developing game packs. It lives in `src/cli/` and uses the same
`Stgadgets.Core` library as the server (`src/lib/`), so it decodes memory and runs `derived.js` exactly as the
server does, without the server or an emulator.

Run it as `bin/stgadgets` from anywhere in the repository. The script builds the CLI from source when it has
changed (quietly; about a second when it hasn't) and runs it, so it's never out of date:

```sh
bin/stgadgets <command>
bin/stgadgets --help
```

With [direnv](https://direnv.net), the repository's `.envrc` puts `bin/` on your PATH whenever you're inside it, so
it's just `stgadgets`. Once, after installing direnv: add `eval "$(direnv hook zsh)"` to `~/.zshrc` (or your shell's
equivalent), then run `direnv allow` in the repository. Below, `stgadgets` means either.

`dotnet test src/cli` builds and tests it.

## Recordings

Recordings are local and git-ignored ([ADR 0011](decisions/0011-recordings-are-local.md)); the `game-recording`
Claude skill explains the workflow. `record` makes one; the other commands read one from its folder. Inside a pack
(`packs/<id>/recordings/<name>`) they use the pack's map and `derived.js` too.

### `stgadgets recording record <recording>`

Records memory from an emulator every frame while someone plays, into a new recording folder. `--provider` picks
the emulator, by the platform's provider IDs; for now that's `retroarch`. Into a pack's `recordings/` folder, it
uses the pack's map and its settings for that provider (`providers/<id>.yaml`; for RetroArch, the core profile), as
the server does:

```sh
stgadgets recording record packs/invaders/recordings/play-02 --provider retroarch --range work_ram=0x2000:0x400 \
  --watch p1.score --watch 0x21FF:u8:spare_ships --seconds 300
```

```
Recording work_ram 0x2000..0x23FF from fb_alpha, invaders: 1,024 bytes a tick, about 3.7 MB a minute.
Stop with Ctrl+C or `touch …/play-02/STOP`; mark with `echo "label" > …/play-02/MARK`.
182 ticks in 3.0 s (TimeUp); interval p50 16.5 ms, p95 17.9 ms; 1 marker(s)
```

| Option | |
|---|---|
| `--provider <id>` | The emulator to record from (required): `retroarch` |
| `--endpoint` | Where the emulator listens, in the provider's form. RetroArch: `host:port`, by default `127.0.0.1:55355` |
| `--settings <file>` | The provider's settings, shaped like a pack's `providers/<id>.yaml`: for a game whose pack has none yet, or to try different ones ([providers.md](providers.md)) |
| `--range [name=]start:size` | Memory to record; repeat for several regions. Without it, what the pack's map reads: small, and enough to check the map against play. Give the work RAM to hunt for new addresses |
| `--watch <name or address[:type[:name]]>` | Logs a value to `changes.log` each time it changes: a map variable, or an address read as a `map.yaml` type (`u8` by default) in the map's byte order (`--endianness` to override) |
| `--seconds` | Stops after this long (600) |
| `--notes` | What happens in the recording, for the manifest |

If the provider can't read the game as set up (for RetroArch, with no core profile for the running core), it says
so and no recording is made. While it runs, writing a label to a file named `MARK` in the recording's folder marks the latest tick, and creating
a file named `STOP` ends the recording, so another process (such as a Claude session) can mark and stop it. It also
stops on Ctrl+C, when the game changes, or when RetroArch stops answering. The manifest is written when it stops.
The interval between snapshots should be about a frame (16.7 ms); much more means frames were missed.

The server can run at the same time: both only read, but each costs RetroArch commands, which it answers once a
frame.

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

## Development

`stgadgets dev` runs stgadgets for development: the server, live or replaying, and on macOS the overlay with
snapshots on. Each part runs as its own process, in the background, as it would for a user; the CLI launches them,
waits until they answer, and talks to them over HTTP. Their output and the record of what's running go to
`.stgadgets/dev/` (git-ignored). Run it from inside the repository.

```sh
stgadgets dev start --overlay                     # live from RetroArch, with the overlay
stgadgets dev start --provider replay --endpoint "packs/ddonpach/recordings/play-01#chain reaches 14"
stgadgets dev status
stgadgets dev restart --build-ui                  # after a change; options not given are kept
stgadgets dev snapshot --width 1000               # the overlay's picture, to .stgadgets/dev/snapshot.png
stgadgets dev stop
```

| Command | |
|---|---|
| `start` | Starts the server, waits until it answers, then gives it a few seconds to find the game, and says what it's doing. A replay held at a point (`#<marker>`) is ready once it's there. With `--overlay`, also starts the overlay and waits for its snapshots. Refuses if something is already serving on the server's port |
| `restart` | Stops and starts again, for a change to `map.yaml`, `derived.js` or the UI. Options not given keep their last values; a different `--provider` starts without the last endpoint |
| `status` | What was started, what the server's doing (`GET /api/status`: state, pack, ticks sent), and whether the overlay answers |
| `snapshot` | Saves a PNG of the layout as the overlay draws it, even while it's hidden (`--out`, `--width`) |
| `stop` | Stops what `start` started |

| Option (start, restart) | |
|---|---|
| `--provider`, `--endpoint` | The emulator, as in the server's configuration ([providers.md](providers.md#choosing-a-provider)); default `retroarch`. A replay's recording is relative to the repository |
| `--overlay` | Also run the macOS overlay, with snapshots on |
| `--snapshot-port` | The overlay's snapshot port (5198) |
| `--build-ui` | Run `npm run build` in `src/ui` first, so the server and overlay serve the latest UI |

When something doesn't start, the command says so with the end of its log.

## Planned

- `stgadgets pack check <dir>`: check a pack against the schema
- `stgadgets pack test <dir>`: run a pack's scenarios and recording checks, for a pack anywhere on disk
