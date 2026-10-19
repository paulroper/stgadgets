// Game states for the overlay's tests: every variable the layout reads, as the server sends them (map and derived.js
// alike). `chaining` was taken from real play; the others start from it and change what that moment changes.

import type { GameStates } from "@stgadgets/testing";

interface Split {
  stage: string;
  frames: number;
  slow: number;
  points: number;
  deaths: number;
  bombs: number;
  continues?: number;
}

const splits = (...done: Split[]) =>
  JSON.stringify(
    done.map(({ stage, frames, slow, points, deaths, bombs, continues = 0 }) => ({
      stage,
      frames,
      game_frames: Math.round(frames * (1 - slow)),
      points,
      deaths,
      bombs,
      continues,
    })),
  );

/** Stage 1, first credit, ship B: a 14-hit chain just landed. */
const chaining = {
  "game.running": true,
  "game.paused": false,
  "game.speed": 1,
  loop: 0,
  stage: 1,
  hiscore: 5908065,

  "p1.score": 10500,
  "p1.score_popup": 1300,
  "p1.lives": 3,
  "p1.more_lives": true,
  "p1.next_life_at": 6000000,
  "p1.bombs_held": 3,
  "p1.shot_level": 0,
  "p1.laser_level": 0,
  "p1.style": 0,
  "p1.chain": 14,
  "p1.chain_gauge": 1,
  "p1.max_hits": 14,
  "p1.deaths_first_credit": 0,
  "p1.no_miss_bonus": 200000,
  "p1.star_bonus": 0,
  "p1.stage_clear_bonus": 200000,

  rank: 25,
  "rank.model": 25,
  "rank.heading_to": null,
  "rank.from_stage": 16,
  "rank.from_loop": 0,
  "rank.from_ships": 3,
  "rank.from_lives": 6,
  "rank.from_bees": 0,
  "rank.from_time": 0,
  "rank.change": -9,
  "rank.change_age": 528,
  "rank.change_reason": "ship & power -9",
  "rank.time_point_in": 27.158688,

  "bees.collected": 0,
  "bees.total": 13,
  "bees.next_value": 100,
  "bees.perfect": 0,
  "bees.perfect_possible": true,
  "bees.stage1": 0,
  "bees.stage2": null,
  "bees.stage3": null,
  "bees.stage4": null,
  "bees.stage5": null,
  "bees.stage6": null,
  "bees.stage7": null,

  "boss.progress": 0.484375,
  "boss.eta": 73.396224,

  "loop2.eligible": true,
  "loop2.possible": true,
  "loop2.hits_needed": 300,
  "loop2.by_deaths": true,
  "loop2.by_hits": false,
  "loop2.by_score": false,
  "loop2.by_bees": false,

  "split.stage": "1-1",
  "split.done": "[]",
  "split.frames": 529,
  "split.game_frames": 529,
  "split.points": 10500,
  "split.deaths": 0,
  "split.bombs": 0,
  "split.continues": 0,
} satisfies GameStates[string];

/** Nothing in play: derived values are null, and memory holds the last game's leftovers. */
const outOfPlay: GameStates[string] = Object.fromEntries(Object.keys(chaining).map((name) => [name, null]));

export default {
  chaining,

  /** The title and ship select screens: only memory that outlives a game, such as the high score. */
  titleScreen: {
    ...outOfPlay,
    "game.paused": false,
    loop: 0,
    hiscore: 5908065,
    rank: 0,
    "bees.perfect": null,
    "split.done": "[]",
  },

  /** Stage 4's boss is on screen: three stages split, two of them perfect for bees, and the game slowing down. */
  bossFight: {
    ...chaining,
    stage: 4,
    "game.speed": 0.62,
    "p1.score": 24815300,
    "p1.score_popup": null,
    "p1.lives": 2,
    "p1.more_lives": false,
    "p1.next_life_at": null,
    "p1.bombs_held": 2,
    "p1.shot_level": 4,
    "p1.laser_level": 4,
    "p1.chain": 0,
    "p1.chain_gauge": 0,
    "p1.max_hits": 187,
    "p1.deaths_first_credit": 1,
    "p1.no_miss_bonus": 500000,
    "p1.star_bonus": 41500,
    "p1.stage_clear_bonus": 541500,
    rank: 52,
    "rank.model": 52,
    "rank.from_stage": 28,
    "rank.from_ships": 15,
    "rank.from_lives": 4,
    "rank.from_bees": 5,
    "rank.from_time": 0,
    "rank.change": 1,
    "rank.change_age": 40,
    "rank.change_reason": "bees +1",
    "rank.time_point_in": 12.4,
    "bees.collected": 5,
    "bees.next_value": 2000,
    "bees.perfect": 2,
    "bees.perfect_possible": false,
    "bees.stage1": 13,
    "bees.stage2": 13,
    "bees.stage3": 9,
    "bees.stage4": 5,
    "boss.progress": 1,
    "boss.eta": 0,
    "loop2.by_hits": false,
    "split.stage": "1-4",
    "split.done": splits(
      { stage: "1-1", frames: 8410, slow: 0.04, points: 3120400, deaths: 0, bombs: 0 },
      { stage: "1-2", frames: 9022, slow: 0.11, points: 6870150, deaths: 0, bombs: 1 },
      { stage: "1-3", frames: 9675, slow: 0.08, points: 9902410, deaths: 1, bombs: 2 },
    ),
    "split.frames": 7230,
    "split.game_frames": 6312,
    "split.points": 4922340,
    "split.deaths": 0,
    "split.bombs": 0,
  },

  /** Rank is about to rise: the formula has moved ahead of rank and the stage is scrolling. */
  rankRising: {
    ...chaining,
    "rank.model": 29,
    "rank.heading_to": 29,
    "rank.from_ships": 7,
    "rank.change": 4,
    "rank.change_age": 10,
    "rank.change_reason": "ship & power +4",
    "p1.shot_level": 2,
    "p1.laser_level": 2,
  },

  /** All 13 bees this stage. */
  perfectBees: {
    ...chaining,
    stage: 2,
    "bees.collected": 13,
    "bees.next_value": null,
    "bees.stage1": 13,
    "bees.stage2": 13,
    "bees.perfect": 1,
    "rank.from_bees": 13,
    "split.stage": "1-2",
  },

  /** The second loop out of reach: three deaths, and a continue. Every extend given. */
  continued: {
    ...chaining,
    stage: 5,
    "p1.score": 18304120,
    "p1.lives": 1,
    "p1.more_lives": false,
    "p1.next_life_at": null,
    "p1.deaths_first_credit": 3,
    "p1.no_miss_bonus": 0,
    "p1.stage_clear_bonus": 0,
    "loop2.eligible": false,
    "loop2.possible": false,
    "loop2.by_deaths": false,
    "split.stage": "1-5",
    "split.done": splits(
      { stage: "1-1", frames: 8410, slow: 0.04, points: 3120400, deaths: 1, bombs: 0 },
      { stage: "1-2", frames: 9022, slow: 0.11, points: 5870150, deaths: 1, bombs: 1 },
      { stage: "1-3", frames: 9675, slow: 0.08, points: 4902410, deaths: 0, bombs: 2 },
      { stage: "1-4", frames: 9320, slow: 0.13, points: 4204330, deaths: 2, bombs: 3, continues: 1 },
    ),
    "split.continues": 0,
  },

  /** The continue screen after the last life: values that last the whole credit are kept. */
  continueScreen: {
    ...outOfPlay,
    "game.running": false,
    "game.paused": false,
    loop: 0,
    hiscore: 5908065,
    rank: 18,
    "p1.max_hits": 17,
    "p1.deaths_first_credit": 0,
    "bees.perfect": 0,
    "bees.stage1": 0,
    "loop2.eligible": true,
    "loop2.possible": true,
    "loop2.hits_needed": 270,
    "loop2.by_deaths": true,
    "loop2.by_hits": false,
    "loop2.by_score": false,
    "loop2.by_bees": false,
    "split.stage": "1-1",
    "split.done": "[]",
    "split.frames": 1981,
    "split.game_frames": 1981,
    "split.points": 55800,
    "split.deaths": 3,
    "split.bombs": 1,
    "split.continues": 0,
  },

  /** Paused, in the game or the emulator. */
  paused: {
    ...chaining,
    "game.paused": true,
  },

  /** Stage 2-3: the second loop's conditions no longer apply, and its rank is 32 higher. */
  secondLoop: {
    ...chaining,
    loop: 1,
    stage: 3,
    "p1.score": 61240880,
    "p1.more_lives": false,
    "p1.next_life_at": null,
    "p1.max_hits": 312,
    "p1.no_miss_bonus": 4000000,
    "p1.stage_clear_bonus": 4000000,
    rank: 63,
    "rank.model": 63,
    "rank.from_stage": 24,
    "rank.from_loop": 32,
    "rank.from_ships": 9,
    "bees.perfect": 5,
    "bees.stage1": 13,
    "bees.stage2": 11,
    "bees.stage3": 0,
    "loop2.eligible": null,
    "loop2.possible": null,
    "loop2.hits_needed": null,
    "loop2.by_deaths": null,
    "loop2.by_hits": null,
    "loop2.by_score": null,
    "loop2.by_bees": null,
    "split.stage": "2-3",
    "split.done": splits(
      { stage: "1-1", frames: 8410, slow: 0.04, points: 3120400, deaths: 0, bombs: 0 },
      { stage: "1-2", frames: 9022, slow: 0.11, points: 6870150, deaths: 0, bombs: 1 },
      { stage: "1-3", frames: 9675, slow: 0.08, points: 9902410, deaths: 0, bombs: 2 },
      { stage: "1-4", frames: 9320, slow: 0.13, points: 11204330, deaths: 0, bombs: 0 },
      { stage: "1-5", frames: 9911, slow: 0.1, points: 10022900, deaths: 1, bombs: 1 },
      { stage: "1-6", frames: 10544, slow: 0.07, points: 12430100, deaths: 0, bombs: 3 },
      { stage: "2-1", frames: 8530, slow: 0.06, points: 3990120, deaths: 0, bombs: 0 },
      { stage: "2-2", frames: 9104, slow: 0.12, points: 3109400, deaths: 0, bombs: 1 },
    ),
  },
} satisfies GameStates;
