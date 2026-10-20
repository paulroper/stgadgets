---
name: latency-test
description: Run the end-to-end latency test (requirement L2) - the user screen-records a game with its on-screen score and the overlay's score gadget, then measure how many frames the gadget is behind or ahead of the game with `stgadgets latency analyse`, check it against the L2 targets and record the result in docs/latency.md. Use when measuring or re-checking overlay latency, e.g. after a change to a provider, the server, the UI or a host, or on a new OS or display mode.
---

# End-to-end latency test

The test for [requirement L2](../../../docs/requirements.md#l2-the-overlay-must-keep-up-with-the-game): how far the score gadget is behind (or ahead of) the game's own score on screen, in display frames at 60 Hz. A screen recording works because both scores are in the same captured frame, so the capture's own delay cancels out.

The tool is `stgadgets latency analyse` (`docs/cli.md#latency`). Run it as `bin/stgadgets` (or `stgadgets` with direnv). Space Invaders is the reference game, but any game with an on-screen score and a score gadget will do.

## 1. Prerequisites

- **ffmpeg** (`ffmpeg -version`; on macOS `brew install ffmpeg`).
- RetroArch running the game, and stgadgets live with the overlay: `stgadgets dev start --overlay` (`stgadgets dev status` to check). The score gadget must be visible over or beside the game, not covering the game's score.
- **Settle the setup with the user and write it down**, since it goes in the results: OS version, RetroArch version and video driver (`video_driver` in `retroarch.cfg`), core, provider, which display and its refresh rate, windowed or fullscreen, and on macOS whether Game Mode is on.
- **The display must run at 60 Hz** for the baseline, which is what L2 is defined in. At 120 or 144 Hz, Cmd-Shift-5 can't keep up with the display (one capture about every 15 ms), so timestamps are only accurate to about a game frame. Set it in System Settings → Displays → Refresh rate. A high-refresh display is worth testing as a separate result. The overlay's `rAF` rate in the server log (`Latency (overlay)` lines) shows the rate it's really painting at. In fullscreen on macOS, Game Mode must be off (menu bar game controller icon → Turn Off Game Mode); with it on the overlay paints at 5 Hz (docs/latency.md).
- **Check RetroArch's latency settings** in `~/Library/Application Support/RetroArch/config/retroarch.cfg`: `run_ahead_enabled`/`run_ahead_frames`, `preemptive_frames_enable`, `vrr_runloop_enable`, `video_vsync`. Run-ahead and preemptive frames show the picture N frames ahead of the core's memory, which `READ_CORE_RAM` can't see, so the gadget is behind by N frames on top of our own lag. Turn them off for the baseline. Testing them on is a separate result.
- Test one display mode per recording. A full test is windowed, then fullscreen.

## 2. The user records

Ask the user to:

1. Start a screen recording. On macOS: **Cmd-Shift-5** → *Record Selected Portion* around the game and the overlay (smaller files, faster analysis), or *Record Entire Screen*. Not *Record Selected Window*: that records only the emulator's window, without the overlay, which is a window of its own. Options → turn off *Show Mouse Clicks*, and keep the pointer away from both scores.
2. Play for about **2 minutes, scoring as often as possible**: 50 or more score changes is a good sample. Space Invaders' attract demo doesn't score, so it needs real play.
3. Stop the recording (the stop button in the menu bar) and give you the file's path.

Alternatively, record with ffmpeg: `ffmpeg -f avfoundation -list_devices true -i ""` lists the screens, then `ffmpeg -f avfoundation -capture_cursor 0 -framerate 60 -i "<screen>:none" -c:v libx264 -crf 18 latency.mp4`, stopped with `q`. Its capture is extra work while the user plays, so prefer Cmd-Shift-5.

Check the video: `ffprobe -v error -select_streams v:0 -show_entries stream=width,height,avg_frame_rate:format=duration -of default=nw=1 <video>`. Screen recordings often have a variable frame rate, which is fine: `analyse` uses each frame's own timestamp.

## 3. Find the two regions

Regions are `x,y,width,height` **in the video's pixels**, not screen points. Retina recordings are at twice the size in points. Work them out from a frame of the recording itself, never from a separate screenshot. Put the frames in your scratchpad.

1. Take a frame from the middle of the recording, when both scores are non-zero, with a 100-pixel grid to read coordinates from:
   ```sh
   ffmpeg -v error -y -ss 60 -i <video> -frames:v 1 frame.png
   ffmpeg -v error -y -i frame.png -vf "drawgrid=w=100:h=100:t=2:c=red@0.6" grid.png
   ```
   Look at `grid.png` with Read. Large frames are scaled down when you view them, so use it only for rough positions.
2. Crop each region at full size and look at it: `ffmpeg -v error -y -i frame.png -vf "crop=<w>:<h>:<x>:<y>" game.png`. Adjust until it fits.
   - **Game:** the player's score digits only, with a few pixels of margin. Not the label, the high score or anything that moves past (in Space Invaders the mystery ship flies just below the scores).
   - **Gadget:** the score digits only. Leave out the label and the `+N` popup beside the score: it disappears on its own later, which would count as an extra change.
3. Show the user both crops by their absolute paths and ask them to confirm.

## 4. Analyse

```sh
stgadgets latency analyse <video> --game <x,y,w,h> --gadget <x,y,w,h>
```

It prints the video's frame interval, the number of changes in each region and how many it paired, and the gadget's offset from the game (+ is behind, − is ahead) as p50, p95, min and max in frames, with a count per whole frame.

Check the result before trusting it:

- **The frame interval should be about 16.7 ms.** If it warns that the video has fewer frames than the display, the gaps can't be measured to a frame. Record again with a smaller area, or with ffmpeg at 60 fps.
- **Nearly every change should be paired.** A few unpaired changes are expected at the start and the end (and a high score being beaten adds none, since it's a different region). Many unpaired changes mean a region is wrong or something else changes inside it. Run again with `--events`, take frames at the unpaired times (`-ss <time>`), and look at what changed. Adjust the region, or `--min-changed` (the percentage of a region's pixels that must change, default 0.5).
- **Offsets beyond a few frames** mean pairs are matched wrongly or something is badly wrong. Look at those times in the video before reporting them.

## 5. Check against the targets and record the result

The [L2 targets](../../../docs/requirements.md#l2-the-overlay-must-keep-up-with-the-game) are for the **gap** (the offset in either direction):

| Provider | p95 target | Hard limit (max) |
|---|---|---|
| Frame-synced (pushes once per frame) | ≤ 1 frame | 2 frames |
| Polling (e.g. RetroArch network commands) | ≤ 2 frames | 3 frames |

Tell the user the verdict. Then add a dated section to the top of `docs/latency.md` in its existing style: the setup from step 1, a table per display mode (changes paired, offset p50 / p95 / min / max, gap p95 and max, per-frame counts), the verdict against the targets, and anything surprising. Describe the findings without naming the local video file. If this completes a roadmap item (`docs/roadmap.md`), tick it.

The video is the user's: leave it where it is, but delete the frames and crops in your scratchpad.
