# Developing a game pack

How a game goes from nothing to a working pack, in three steps: build the memory map, decide which gadgets are
worth having, then build them. It's the workflow the [DoDonPachi pack](../packs/ddonpach/) was made with, and its
[`NOTES.md`](../packs/ddonpach/NOTES.md) is a worked example of the research. [Game packs](packs.md) has the
reference for what goes in a pack; this page is about how to fill one.

## 1. Build the memory map

The goal is a `map.yaml` whose addresses are known to be right, and a `NOTES.md` that says how each was found.

### Start from published research

Most popular arcade games have been studied already. Check these before searching memory yourself:

| Source | What it's good for | Watch out for |
|---|---|---|
| **RetroAchievements code notes** | Annotated addresses for score, lives, stage, game state and much more | Addresses are in the emulator core's layout, not the hardware's. On FBNeo's 68000 games, 8-bit notes point at the other byte of each word. The notes are readable without an API key (`dorequest.php?r=codenotes2&g=<game id>`) |
| **Reverse-engineering write-ups** | How the game works: rank formulas, slowdown, bugs | Often for a different ROM set (DoDonPachi's are for the Japanese set) |
| **Open-source trainers, cheats and patches** | Labelled addresses, in the original hardware's space | Tied to one ROM set; patches change the ROM, so the pack won't match the patched game |
| **MAME source** (the driver) | The memory map, ROM sets and CRCs, the screen's refresh rate | |
| **Community wikis** (e.g. the shmups wiki) | The game's rules: scoring, bonuses, loop conditions | Rules, not addresses; check them against the code |

### Attribution and licensing

**Every piece of research we use is credited, and its licence must allow us to use it.**

- List each source in the pack's `NOTES.md` (a table with what it gave and its licence), and credit it in the map's
  comments or descriptions where its addresses are used.
- Check the licence before using anything. Public domain, and permissive or copyleft licences such as GPL-3.0,
  are fine when we follow their terms. With no stated licence, take only facts, such as addresses and rules, never
  code, text or logic copied wholesale (RetroAchievements' code notes are used this way). If the terms are unclear,
  ask before using it.
- Never copy ROM data into the repo. Reading your own copy of a ROM to check a routine is fine; the ROM stays out of
  the repo, and so do disassembly dumps of it.

### Check everything against the game's own code

Published addresses are a starting point, and they're often for another ROM set or another emulator's layout. Before
trusting one:

1. **Match the ROM set.** Confirm each address is used by the program ROMs of the set the pack is for: read your own
   copy of the ROM, disassemble it (Capstone does 68000), and find the code that reads or writes the address. When
   the code is found, note its address in the map (`code at 0x3362`), so the next person can find it too.
2. **Read the routines that matter.** A formula or rule from a write-up should be checked against the code that
   implements it. For DoDonPachi this found details no write-up mentioned (rank drops a dying player's terms, and
   the World set's rank tables differ from the Japanese set's).
3. **Check against the screen.** Read the values live while the game runs and compare them with what's shown:
   score, lives, bombs.

### Record and verify

Record play with the `game-recording` Claude skill (`.claude/skills/game-recording/`) and a short script of events:
start a credit, lose a life, use a bomb, and so on. Then:

- Search the recording for addresses the research didn't have.
- Confirm each variable's values over time, and set `verified: true` on the ones that check out.
- Write the recording's `expected.yaml`, so `dotnet test src/server` checks the map (and `derived.js`) against it.

Recordings are local working material and are never committed ([ADR 0011](decisions/0011-recordings-are-local.md)).
So committed files describe what was found, not where: "matched the formula at every change in a recorded session",
never a recording's name or tick numbers. Only a recording's own `expected.yaml`, which stays with it, refers to its
ticks.

### Write it down

The pack's `NOTES.md` is where the research lives: the sources, how the game stores things (byte order, values kept
in a word's low byte, leftovers between games), each rule with the routine it comes from, disagreements between
sources, and what's still unchecked. Keep it up to date as you learn, and mark what's verified and what isn't.

## 2. Decide which gadgets are worth having

With the map in place, think about what a player of this game would want to see that the game doesn't show. Good
gadgets usually come from:

- **The game's hidden systems.** Rank, chain timers, bonus conditions and loop requirements are things players track
  in their heads; showing them is the point of stgadgets.
- **Forecasts.** What the stage clear bonus will be, when the next extend comes, how long until the boss.
- **What happens over time.** Splits per stage, the score from the latest hit, slowdown.
- **The game's own rules,** found while building the map. Many of DoDonPachi's gadgets came straight out of reading its
  code (the bee values, the second loop check, the no-miss bonus table).

Keep within these limits:

- **Gadgets sit beside the game, not on it.** Nothing that draws over the action or would feel like cheating, such
  as hitbox overlays, without a decision to allow it.
- **Only what memory supports.** If a value can't be read or derived reliably, leave it out or say it's approximate.
- **Latency first.** Every derived value is computed every frame ([requirements](requirements.md)).

Write the ideas down, and put the ones you don't build yet in the pack's `NOTES.md` under future ideas.

## 3. Build the gadgets

### Derived values first

Put the game logic in the pack's `derived.js`, not in the gadgets
([memory maps → derived values](memory-maps.md#derived-values)). Gadgets then only display values, and the logic can
be tested on its own:

- Gate everything on `game.running`, and use the standard names where they fit.
- Write scenarios for each rule in `tests/derived/` ([testing derived values](packs.md#testing-derived-values)),
  including its edges: the tick a threshold is crossed, outside play, the second player. Write them from the rule
  (the routine in the ROM, or the write-up), not from what `derived.js` happens to return.
- Add the new values to a recording's `expected.yaml` too, so real play checks them. Run `dotnet test src/server`.
- Keep an eye on cost: hundreds of values each doing little is fine, but many near-identical values (one per stage
  per field, say) are better as one structured value.

### Then the layout and gadgets

Write the pack's `ui/layout.tsx` and its gadgets in `ui/gadgets/`, then `npm run build` and `npm test` in `src/ui`.
See [gadgets & layouts](gadgets.md), and follow the [gadget design](gadget-design.md) language.

Add a game state to the pack's `tests/states.ts` for each moment a gadget behaves differently in (a boss, a
continue, a perfect stage), and a test that shows the layout in it ([testing the UI](packs.md#testing-the-ui)). This
checks gadgets at moments that are hard to reach in play, without the server or an emulator.

### The build-and-look loop

To see changes as they'll really appear, run the server and the macOS overlay with snapshots on:

```sh
dotnet run --project src/server/Stgadgets.Server
STGADGETS_SNAPSHOT_PORT=5198 dotnet run --project src/host/macos/Stgadgets.Host.Mac
```

Then each change goes:

1. Edit `derived.js`, the layout or a gadget.
2. Rebuild the UI (`npm run build` in `src/ui`), and restart the server if `map.yaml` or `derived.js` changed (it
   loads packs at startup). Then use **Reload layout** in the overlay's menu, or restart the overlay, which needs
   nobody at the screen:

   ```sh
   pkill -f Stgadgets.Host.Mac.app
   STGADGETS_SNAPSHOT_PORT=5198 dotnet run --project src/host/macos/Stgadgets.Host.Mac
   ```

   `dotnet run` opens the app and returns. Snapshots work a few seconds later, once the port answers.

   A freshly started overlay takes its size from the emulator's window, and only finds it while it's on screen. If
   RetroArch is fullscreen (in its own Space), bring it to the front once after restarting the overlay, or snapshots
   come out at a default 400×300. The overlay keeps that size afterwards, even while hidden.
3. Fetch `http://127.0.0.1:5198/snapshot` (add `?width=1000` for a smaller image) and look at it.

The snapshot is the layout as the overlay draws it, at the emulator window's size, with live values from the running
game. It works while the overlay is hidden behind another app, so a Claude session can make a change and check it
without anyone looking at the screen. It shows the layout only, not the game underneath.

When a gadget needs a particular moment in the game (a boss, a death, a continue), ask whoever is playing to get
there, or check the logic against a recording first and the look in a snapshot afterwards.

### Checking other window sizes

The layout scales with the window's height ([gadget design](gadget-design.md#layout)), so check it fits in small and
large windows too, without resizing the emulator. With the server running a game:

```sh
cd src/ui
npx playwright install firefox     # once
npm run screenshot                  # 1024x600, 1512x917 and 2560x1440
npm run screenshot -- 1280x720      # or any sizes
```

It opens the layout in a headless Firefox at each size, saves `src/ui/screenshots/<size>.png` (git-ignored), and prints
whether each side fits:

```
1024x600: 1rem = 12px; layout-left 206px wide, overflows by 45px; layout-right 206px wide, fits
```

Firefox lays out text slightly differently from the overlay's webview, so confirm the final look in a snapshot.

### Looking at game states

To see the layout at a moment that's hard to reach in play, show one of the pack's game states
([testing the UI](packs.md#testing-the-ui)) instead of the running game. It needs neither the server nor an emulator:

```sh
cd src/ui
npm run screenshot -- --state ddonpach/bossFight    # one state, at the three default sizes
npm run screenshot -- --state ddonpach 1512x917     # every state of the pack, at one size
```

Each picture is saved as `src/ui/screenshots/<pack>-<state>-<size>.png`. In `npm run dev`, open
`/?state=ddonpach/bossFight` to see a state in a browser, with hot reload. Looking at every state after a change shows
each gadget in context, and catches states whose values don't add up.
