// Game states for the overlay's tests and for looking at the layout (docs/packs.md#testing-the-ui). Each is a run of
// ticks of map values; the pack's derived.js works out everything else, as the server would.

import type { GameStates } from "@stgadgets/testing";

export default {
  // The start of stage 1 in real play (solo, first credit, ship B, three lives), with power at 0 and a few points.
  start: {
    "game.state": 0,
    "game.phase": 0,
    frame_counter: 1000,
    "game.flags": 0xe0,
    credits: 0,
    "stage.index": 0,
    loop: 0,
    "stage.progress": 0,
    rank: 25,
    "rank.half": 12,
    "rank.quarter": 6,
    "rank.over_32": 0,
    "rank.survival_frames": 0,
    "bees.stage": 0,
    "p1.stars_large": 0,
    "p1.stars_small": 0,
    "p2.stars_large": 0,
    "p2.stars_small": 0,
    "bees.perfect_stages": 0,
    "settings.dips": 0,
    "p1.score_digits": 920,
    "p2.score_digits": 0,
    hiscore_digits: 590806,
    "hiscore.last_digit": 5,
    "p1.continues": 0x100,
    "p2.continues": 0x100,
    "p1.hit_timer": 0,
    "p2.hit_timer": 0,
    "chain.timer_reload": 56,
    "p1.hits": 0,
    "p2.hits": 0,
    "p1.max_hits": 0,
    "p2.max_hits": 0,
    "boss.hits": 0,
    "p1.stock": 3,
    "p2.stock": 0,
    "p1.next_extend": 0x600000,
    "p2.next_extend": 0x600000,
    "p1.deaths_stage": 0,
    "p2.deaths_stage": 0,
    "p1.deaths_first_credit": 0,
    "p2.deaths_first_credit": 0,
    "p1.bombs": 3,
    "p1.bomb_slots": 3,
    "p2.bombs": 0,
    "p2.bomb_slots": 0,
    "p1.max_bonus": 0,
    "p2.max_bonus": 0,
    "p1.flags": 0x80,
    "p2.flags": 0,
    "p1.ship": 1,
    "p2.ship": 0,
    "p1.style": 0,
    "p2.style": 0,
    "p1.shot_power": 0,
    "p2.shot_power": 0,
    "p1.laser_power": 0,
    "p2.laser_power": 0,
    "p1.invincibility": 0,
    "p2.invincibility": 0,
  },
  // The game's frame counter moves every tick: full speed. A death sets p1.flags to 0x01 (exploding) and stock drops
  // at the respawn, as in the game.
  add: { frame_counter: 1 },

  states: {
    /** Nine seconds into stage 1, a 14-hit chain has just landed. */
    chaining: [
      { repeat: 528 },
      { set: { "p1.hit_timer": 55, "p1.hits": 14, "p1.max_hits": 14, "p1.score_digits": 1050, "stage.progress": 248 } },
    ],

    /** The ship select screen: memory still holds the last game's score and lives. */
    titleScreen: [
      { set: { "game.state": 11, "p1.flags": 0, "p1.stock": 0, "p1.bombs": 0, rank: 0, "p1.score_digits": 324 } },
    ],

    /** A power-up: the formula has moved ahead of rank, and the stage is scrolling, so rank is about to follow. */
    rankRising: [{ repeat: 100 }, { set: { "p1.shot_power": 2, "p1.laser_power": 2, "stage.progress": 10 } }],

    /** Every bee on stages 1 and 2. */
    perfectBees: [
      { set: { "bees.stage": 13 }, add: { "p1.score_digits": 50 }, repeat: 300 },
      {
        set: { "stage.index": 1, "bees.stage": 0, "bees.perfect_stages": 1 },
        add: { "p1.score_digits": 50 },
        repeat: 300,
      },
      { set: { "bees.stage": 13, rank: 44 } },
    ],

    /**
     * Stage 4's boss after three stages: every bee on 1 and 2, a bomb on 2, a death on 3, full power, and the game
     * slowing down.
     */
    bossFight: [
      { set: { "bees.stage": 13 }, add: { "p1.score_digits": 150 }, repeat: 1500 },
      { set: { "stage.index": 1, "bees.stage": 0, "bees.perfect_stages": 1 } },
      { set: { "bees.stage": 13, "p1.bombs": 2 }, add: { "p1.score_digits": 300 }, repeat: 1800 },
      { set: { "stage.index": 2, "bees.stage": 0, "bees.perfect_stages": 2, "p1.shot_power": 4, "p1.laser_power": 4 } },
      { set: { "bees.stage": 9 }, add: { "p1.score_digits": 400 }, repeat: 2000 },
      { set: { "p1.flags": 0x01, "p1.deaths_stage": 1, "p1.deaths_first_credit": 1, "bees.stage": 0 }, repeat: 90 },
      { set: { "p1.flags": 0x80, "p1.stock": 2, "p1.bombs": 3 }, repeat: 300 },
      { set: { "stage.index": 3, "p1.deaths_stage": 0, "p1.max_hits": 187, rank: 58 } },
      { set: { "bees.stage": 5 }, add: { "p1.score_digits": 500 }, repeat: 1500 },
      { set: { "stage.progress": 0x200 }, add: { frame_counter: -1 }, repeat: 20 },
      {},
    ],

    /** Stage 5 after the third death and a continue: the second loop and the no-miss bonus are gone, every extend given. */
    continued: [
      {
        set: {
          "stage.index": 4,
          "p1.next_extend": 0xffff,
          "p1.score_digits": 1830412,
          "p1.deaths_first_credit": 2,
          "p1.stock": 1,
        },
        repeat: 600,
      },
      { set: { "p1.flags": 0x01, "p1.deaths_stage": 1, "p1.deaths_first_credit": 3 }, repeat: 90 },
      { set: { "game.state": 8, "p1.flags": 0, "p1.stock": 0 }, repeat: 120 },
      { set: { "p1.continues": 0x101 } },
      { set: { "game.state": 0, "p1.flags": 0x80, "p1.stock": 3, rank: 41 }, repeat: 300 },
    ],

    /** The continue screen after losing every life on stage 1, with a bomb used. */
    continueScreen: [
      { add: { "p1.score_digits": 3 }, repeat: 600 },
      { set: { "p1.flags": 0x01, "p1.deaths_stage": 1, "p1.deaths_first_credit": 1 }, repeat: 90 },
      { set: { "p1.flags": 0x80, "p1.stock": 2 }, repeat: 300 },
      { set: { "p1.bombs": 2 }, repeat: 300 },
      { set: { "p1.flags": 0x01, "p1.deaths_stage": 2, "p1.deaths_first_credit": 2, "p1.max_hits": 17 }, repeat: 90 },
      { set: { "p1.flags": 0x80, "p1.stock": 1 }, repeat: 300 },
      { set: { "p1.flags": 0x01, "p1.deaths_stage": 3, "p1.deaths_first_credit": 3 }, repeat: 90 },
      { set: { "game.state": 8, "p1.flags": 0, "p1.stock": 0 }, repeat: 60 },
    ],

    /** The chain above, then the game (or the emulator) paused. */
    paused: [
      { repeat: 528 },
      { set: { "p1.hit_timer": 55, "p1.hits": 14, "p1.max_hits": 14, "p1.score_digits": 1050, "stage.progress": 248 } },
      { add: { frame_counter: -1 }, repeat: 31 },
    ],

    /** Stage 2-3, after the whole first loop with every bee: the second loop's conditions no longer apply. */
    secondLoop: [
      { set: { "bees.stage": 13 }, add: { "p1.score_digits": 1000 }, repeat: 600 },
      { set: { "stage.index": 1, "bees.perfect_stages": 1 }, add: { "p1.score_digits": 1000 }, repeat: 600 },
      { set: { "stage.index": 2, "bees.perfect_stages": 2 }, add: { "p1.score_digits": 1000 }, repeat: 600 },
      { set: { "stage.index": 3, "bees.perfect_stages": 3 }, add: { "p1.score_digits": 1000 }, repeat: 600 },
      { set: { "stage.index": 4, "bees.perfect_stages": 4 }, add: { "p1.score_digits": 1000 }, repeat: 600 },
      { set: { "stage.index": 5, "bees.perfect_stages": 5 }, add: { "p1.score_digits": 1000 }, repeat: 600 },
      { set: { "stage.index": 6, "bees.perfect_stages": 6 }, repeat: 300 },
      {
        set: { loop: 1, "stage.index": 0, "bees.stage": 0, "p1.max_hits": 312 },
        add: { "p1.score_digits": 1000 },
        repeat: 600,
      },
      { set: { "stage.index": 1 }, add: { "p1.score_digits": 1000 }, repeat: 600 },
      { set: { "stage.index": 2, "stage.progress": 100, rank: 63 }, add: { "p1.score_digits": 1000 }, repeat: 300 },
    ],
  },
} satisfies GameStates;
