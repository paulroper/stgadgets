// Game states for the overlay's tests and for looking at the layout (docs/packs.md#testing-the-ui). Each is a run of
// ticks of map values; the pack's derived.js works out everything else, as the server would.

import type { GameStates } from "@stgadgets/testing";

export default {
  // A new wave, with player 1 up and the rack just below its starting height.
  start: {
    hiscore: 0,
    "p1.score": 0,
    "p2.score": 0,
    credits: 1,
    aliens_remaining: 55,
    "p1.ships_reserve": 2,
    "player.state": 0xff,
    "player.data": 0x21,
    "fleet.sound_delay": 52,
    "rack.y": 0x70,
    "saucer.timer": 0x600,
    "saucer.due": false,
    "saucer.active": false,
    "saucer.score_ptr": 0x54,
    "game.mode": true,
    "game.moving": true,
    "shots.count": 0,
  },

  states: {
    /** 15 invaders left: 40 hit with 58 shots, and the mystery ship is flying and was just hit for 100. */
    saucerFlying: [
      {},
      {
        set: {
          aliens_remaining: 15,
          "shots.count": 58,
          "p1.score": 720,
          "fleet.sound_delay": 21,
          "saucer.score_ptr": 0x56,
          "saucer.active": true,
        },
        repeat: 30,
      },
      { set: { "p1.score": 820 } },
    ],

    /** A new wave: the slowest march, and the mystery ship held until the rack drops. */
    waveStart: [{ set: { "rack.y": 0x78 } }],

    /** The next shot scores 300 if it hits the mystery ship, which is due any moment; the march has just sped up. */
    saucerJackpot: [
      {},
      {
        set: {
          aliens_remaining: 22,
          "shots.count": 45,
          "fleet.sound_delay": 34,
          "saucer.due": true,
          "saucer.score_ptr": 0x5b,
        },
      },
    ],

    /** One invader left, on the last life: the march is at full speed and no more mystery ships come this wave. */
    lastInvader: [
      {},
      { set: { aliens_remaining: 1, "shots.count": 90, "p1.ships_reserve": 0, "fleet.sound_delay": 5 } },
    ],

    /** Game over: play is held still, and the score and the new high score stay on screen. */
    gameOver: [
      {},
      { set: { aliens_remaining: 40, "shots.count": 25, "p1.score": 960 }, repeat: 120 },
      { set: { "game.moving": false, hiscore: 960, "p1.ships_reserve": 0 } },
    ],
  },
} satisfies GameStates;
